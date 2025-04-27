using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SafeObject.Core.Helpers;
using SafeObject.Core.Interfaces;
using SafeObject.Core.Models;
using SafeObject.Core.Services.Factory;

namespace SafeObject.Core.Services;

public sealed class StorageService(ILogger<StorageService>? logger, IVaultService? keyVaultService)
    : IStorageService, IDisposable
{
    private static readonly ArrayPool<byte> SecurePool = ArrayPool<byte>.Create();

    private readonly IVaultService _keyVaultService =
        keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));

    private readonly ILogger<StorageService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private volatile bool _disposed;

    public async Task EncryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        var key = GenerateRandomKey();
        var nonce = SecurePool.Rent(Constants.Security.KeyVault.NonceSize);

        try
        {
            RandomNumberGenerator.Fill(nonce);

            await _keyVaultService.StoreKeyAsync(request.FileId, Convert.ToBase64String(key), filePublicMasterKey);
            await ProcessEncryptionStreamAsync(request, key, nonce, cancellationToken);
        }
        finally
        {
            SecurePool.Return(nonce);
        }
    }

    public async Task DecryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        var filePrivateKey = await _keyVaultService.RetrieveKeyAsync(request.FileId, filePublicMasterKey);
        var key = Convert.FromBase64String(filePrivateKey);

        try
        {
            await using var sourceStream = CreateFileStream(request.SourcePath, FileMode.Open, FileAccess.Read, logger);
            await using var destinationStream =
                CreateFileStream(request.DestinationPath, FileMode.Create, FileAccess.Write, logger);

            await ProcessDecryptionAsync(key, sourceStream, destinationStream, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing && _keyVaultService is IDisposable vaultService)
            vaultService.Dispose();

        _disposed = true;
    }

    ~StorageService()
    {
        Dispose(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed is not true)
            return;

        throw new ObjectDisposedException(nameof(StorageService));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FileStream CreateFileStream(
        string path,
        FileMode mode,
        FileAccess access,
        ILogger<StorageService>? logger)
    {
        var share = access is FileAccess.Read ? FileShare.Read : FileShare.None;
        var options = FileOptions.Asynchronous
                      | FileOptions.SequentialScan
                      | (access is FileAccess.Write ? FileOptions.WriteThrough : FileOptions.None);

        return DirectStreamFactory.Create(
            path,
            mode,
            access,
            share,
            Constants.Storage.BufferSize,
            options,
            logger);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GenerateRandomKey(int keySize = Constants.Security.KeyVault.DefaultKeySize)
    {
        var key = new byte[keySize];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ProcessDecryptionAsync(byte[] key, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nonce = SecurePool.Rent(Constants.Security.KeyVault.NonceSize);
        try
        {
            await sourceStream.ReadExactlyAsync(nonce.AsMemory(0, Constants.Security.KeyVault.NonceSize),
                cancellationToken);

            using var aesGcm = new AesGcm(key, Constants.Security.KeyVault.TagSize);
            await ProcessDecryptionStreamAsync(sourceStream, destinationStream, aesGcm, nonce, cancellationToken);
        }
        finally
        {
            SecurePool.Return(nonce, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeriveNonce(byte[] salt, long blockIndex, byte[] outputNonce)
    {
        var hashProvider = HashAlgorithmProviderFactory.Instance;
        var hmacKeySize = hashProvider.GetHmacKeySize();

        Span<byte> blockIndexBytes = stackalloc byte[sizeof(long)];
        Span<byte> prk = stackalloc byte[hmacKeySize];
        Span<byte> info = stackalloc byte[sizeof(long) + Constants.Security.KeyVault.NonceContext.Length];
        Span<byte> okm = stackalloc byte[Constants.Security.KeyVault.NonceSize];

        try
        {
            BitConverter.TryWriteBytes(blockIndexBytes, blockIndex);

            using (var hmac = hashProvider.CreateHmac(salt))
            {
                hmac.TryComputeHash(blockIndexBytes, prk, out var bytesWritten);
                if (bytesWritten != hmacKeySize)
                    throw new CryptographicException("HMAC computation failed.");
            }

            blockIndexBytes.CopyTo(info);
            Constants.Security.KeyVault.NonceContext.CopyTo(info[sizeof(long)..]);

            HKDF.Expand(hashProvider.GetHashAlgorithmName(), prk, okm, info);
            okm.CopyTo(outputNonce.AsSpan(0, Constants.Security.KeyVault.NonceSize));
        }
        catch (Exception)
        {
            throw new CryptographicException("Failed to derive nonce.");
        }
        finally
        {
            prk.Clear();
            okm.Clear();
            info.Clear();
            blockIndexBytes.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrecomputeSalt(byte[] originalNonce, byte[] salt)
    {
        var hashProvider = HashAlgorithmProviderFactory.Instance;

        Span<byte> input = stackalloc byte[8];
        var saltSize = hashProvider.GetSaltSize();

        try
        {
            BitConverter.TryWriteBytes(input, 0L);

            using var hmac = hashProvider.CreateHmac(originalNonce);
            if (!hmac.TryComputeHash(input, salt, out var bytesWritten) ||
                bytesWritten != saltSize)
                throw new CryptographicException("Failed to derive salt.");
        }
        finally
        {
            input.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask ProcessEncryptionStreamAsync(FileProcessingRequest request, byte[] key, byte[] nonce,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = CreateFileStream(
            request.SourcePath, FileMode.Open, FileAccess.Read, _logger);
        await using var destinationStream = CreateFileStream(
            request.DestinationPath, FileMode.Create, FileAccess.Write, _logger);

        using var aesGcm = new AesGcm(key, Constants.Security.KeyVault.TagSize);

        var saltSize = HashAlgorithmProviderFactory.Instance.GetSaltSize();

        var buffer = SecurePool.Rent(Constants.Storage.BufferSize);
        var ciphertext = SecurePool.Rent(Constants.Storage.BufferSize);
        var tag = SecurePool.Rent(Constants.Security.KeyVault.TagSize);
        var chunkNonce = SecurePool.Rent(Constants.Security.KeyVault.NonceSize);
        var salt = SecurePool.Rent(saltSize);

        try
        {
            PrecomputeSalt(nonce, salt);

            await destinationStream.WriteAsync(nonce.AsMemory(0, Constants.Security.KeyVault.NonceSize),
                cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);

            var totalBlocks = (long)Math.Ceiling((double)sourceStream.Length / Constants.Storage.BufferSize);

            for (long blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, Constants.Storage.BufferSize),
                    cancellationToken);
                if (bytesRead is 0) break;

                DeriveNonce(salt, blockIndex, chunkNonce);

                aesGcm.Encrypt(
                    chunkNonce.AsSpan(0, Constants.Security.KeyVault.NonceSize),
                    buffer.AsSpan(0, bytesRead),
                    ciphertext.AsSpan(0, bytesRead),
                    tag.AsSpan(0, Constants.Security.KeyVault.TagSize));

                await destinationStream.WriteAsync(tag.AsMemory(0, Constants.Security.KeyVault.TagSize),
                    cancellationToken);
                await destinationStream.WriteAsync(ciphertext.AsMemory(0, bytesRead), cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            SecurePool.Return(buffer, true);
            SecurePool.Return(ciphertext, true);
            SecurePool.Return(tag, true);
            SecurePool.Return(chunkNonce, true);
            SecurePool.Return(salt, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ProcessDecryptionStreamAsync(
        Stream sourceStream,
        Stream destinationStream,
        AesGcm aesGcm,
        byte[] nonce,
        CancellationToken cancellationToken)
    {
        var saltSize = HashAlgorithmProviderFactory.Instance.GetSaltSize();

        var tag = SecurePool.Rent(Constants.Security.KeyVault.TagSize);
        var buffer = SecurePool.Rent(Constants.Storage.BufferSize);
        var plaintext = SecurePool.Rent(Constants.Storage.BufferSize);
        var chunkNonce = SecurePool.Rent(Constants.Security.KeyVault.NonceSize);
        var salt = SecurePool.Rent(saltSize);

        try
        {
            PrecomputeSalt(nonce, salt);

            var totalBlocks = (long)Math.Ceiling((double)(sourceStream.Length - Constants.Security.KeyVault.NonceSize) /
                                                 (Constants.Security.KeyVault.TagSize + Constants.Storage.BufferSize));

            for (long blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
            {
                var tagRead = await sourceStream.ReadAsync(tag.AsMemory(0, Constants.Security.KeyVault.TagSize),
                    cancellationToken);
                if (tagRead is 0)
                    break;

                var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, Constants.Storage.BufferSize),
                    cancellationToken);
                if (bytesRead is 0)
                    break;

                DeriveNonce(salt, blockIndex, chunkNonce);

                aesGcm.Decrypt(
                    chunkNonce.AsSpan(0, Constants.Security.KeyVault.NonceSize),
                    buffer.AsSpan(0, bytesRead),
                    tag.AsSpan(0, Constants.Security.KeyVault.TagSize),
                    plaintext.AsSpan(0, bytesRead));

                await destinationStream.WriteAsync(plaintext.AsMemory(0, bytesRead), cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            SecurePool.Return(tag, true);
            SecurePool.Return(buffer, true);
            SecurePool.Return(plaintext, true);
            SecurePool.Return(chunkNonce, true);
            SecurePool.Return(salt, true);
            SecurePool.Return(nonce, true);
        }
    }
}