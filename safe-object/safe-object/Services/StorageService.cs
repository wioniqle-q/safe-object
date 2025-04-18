using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using safe_object.Interfaces;
using safe_object.Models;

namespace safe_object.Services;

public sealed class StorageService(ILogger<StorageService>? logger, IVaultService? keyVaultService)
    : IStorageService, IDisposable
{
    private readonly IVaultService _keyVaultService =
        keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));

    private readonly ILogger<StorageService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private bool _disposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task EncryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateSecurityOperation();

        LogDebugInfo(request.FileId, filePublicMasterKey);

        var filePrivateKey = Convert.ToBase64String(GenerateRandomKey());
        var key = Convert.FromBase64String(filePrivateKey);
        var nonce = new byte[Constants.Security.KeyVault.NonceSize];
        RandomNumberGenerator.Fill(nonce);

        try
        {
            await _keyVaultService.StoreKeyAsync(request.FileId, filePrivateKey, filePublicMasterKey);
            await ProcessEncryptionStreamAsync(request, key, nonce, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task DecryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateSecurityOperation();

        LogDebugInfo(request.FileId, filePublicMasterKey);

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
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
            if (_keyVaultService is IDisposable vaultService)
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
        if (_disposed is not true) return;

        throw new ObjectDisposedException(nameof(StorageService));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateSecurityOperation()
    {
        if (SecurityService.ValidateOperation() is not true)
        {
            SecurityService.ProcessPaddingBuffer();
            throw new SecurityException("Security validation failed");
        }

        Thread.MemoryBarrier();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DirectStream CreateFileStream(string path, FileMode mode, FileAccess access,
        ILogger<StorageService>? logger)
    {
        ValidateSecurityOperation();

        return new DirectStream(path, mode, access, FileShare.None, Constants.Storage.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan |
            (access is FileAccess.Write ? FileOptions.WriteThrough : FileOptions.None), logger);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ProcessDecryptionAsync(byte[] key, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        var nonce = ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.NonceSize);
        try
        {
            await sourceStream.ReadExactlyAsync(nonce.AsMemory(0, Constants.Security.KeyVault.NonceSize),
                cancellationToken);
            using var aesGcm = new AesGcm(key, Constants.Security.KeyVault.TagSize);
            await ProcessDecryptionStreamAsync(sourceStream, destinationStream, aesGcm, nonce, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(nonce, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeriveNonce(byte[] originalNonce, long blockIndex, byte[] outputNonce)
    {
        if (originalNonce is null || outputNonce is null)
            throw new ArgumentNullException(originalNonce is null ? nameof(originalNonce) : nameof(outputNonce));

        if (outputNonce.Length < Constants.Security.KeyVault.NonceSize)
            throw new ArgumentOutOfRangeException(nameof(outputNonce),
                $"Output nonce must be at least {Constants.Security.KeyVault.NonceSize} bytes long");

        try
        {
            Span<byte> blockIndexBytes = stackalloc byte[sizeof(long)];
            if (BitConverter.TryWriteBytes(blockIndexBytes, blockIndex) is not true)
                throw new InvalidOperationException();

            Span<byte> salt = stackalloc byte[32];
            originalNonce.AsSpan(0, Math.Min(originalNonce.Length, Constants.Security.KeyVault.NonceSize)).CopyTo(salt);

            for (var i = Constants.Security.KeyVault.NonceSize; i < salt.Length; i++)
                salt[i] = (byte)(0xAA ^ (i & 0xFF));

            Span<byte> prk = stackalloc byte[32];
            {
                using var hmac = new HMACSHA256();
                hmac.Key = salt.ToArray();

                if (!hmac.TryComputeHash(blockIndexBytes, prk, out var bytesWritten) || bytesWritten is not 32)
                    throw new CryptographicException();

                CryptographicOperations.ZeroMemory(hmac.Key);
            }

            Span<byte> info = stackalloc byte[sizeof(long) + 16];
            blockIndexBytes.CopyTo(info);
            var context = "AES-GCM-NONCE-V1"u8;
            context.CopyTo(info[sizeof(long)..]);

            Span<byte> okm = stackalloc byte[Constants.Security.KeyVault.NonceSize];
            Span<byte> t = stackalloc byte[32];
            Span<byte> input = stackalloc byte[32 + info.Length + 1];
            Span<byte> currentT = stackalloc byte[32];
            var tPos = 0;
            byte counter = 1;

            using (var hmacExpand = new HMACSHA256())
            {
                hmacExpand.Key = prk.ToArray();

                var bytesToProcess = okm.Length;
                var okmPos = 0;

                while (bytesToProcess > 0)
                {
                    input.Clear();
                    if (tPos > 0)
                        t[..tPos].CopyTo(input);

                    info.CopyTo(input[tPos..]);
                    input[tPos + info.Length] = counter++;

                    currentT.Clear();
                    if (!hmacExpand.TryComputeHash(input[..(tPos + info.Length + 1)], currentT, out var bytesWritten) ||
                        bytesWritten is not 32)
                        throw new CryptographicException();

                    var bytesToCopy = Math.Min(bytesToProcess, currentT.Length);
                    currentT[..bytesToCopy].CopyTo(okm[okmPos..]);
                    okmPos += bytesToCopy;
                    bytesToProcess -= bytesToCopy;
                    currentT.CopyTo(t);
                    tPos = currentT.Length;
                }

                CryptographicOperations.ZeroMemory(hmacExpand.Key);
            }

            okm.CopyTo(outputNonce.AsSpan(0, Constants.Security.KeyVault.NonceSize));

            CryptographicOperations.ZeroMemory(prk);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(okm);
            CryptographicOperations.ZeroMemory(t);
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(currentT);
            CryptographicOperations.ZeroMemory(info);
            CryptographicOperations.ZeroMemory(blockIndexBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Nonce derivation failed", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask ProcessEncryptionStreamAsync(FileProcessingRequest request, byte[] key, byte[] nonce,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        await using var sourceStream = new DirectStream(
            request.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            Constants.Storage.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough,
            _logger);

        await using var destinationStream = new DirectStream(
            request.DestinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            Constants.Storage.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough,
            _logger);

        await destinationStream.WriteAsync(nonce.AsMemory(), cancellationToken);
        await destinationStream.FlushAsync(cancellationToken);

        using var aesGcm = new AesGcm(key, Constants.Security.KeyVault.TagSize);

        var fileLength = sourceStream.Length;
        var totalBlocks = (long)Math.Ceiling((double)fileLength / Constants.Storage.BufferSize);

        var buffer = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);
        var ciphertext = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);
        var tag = ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.TagSize);
        var chunkNonce = ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.NonceSize);
        var combinedBuffer =
            ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.TagSize + Constants.Storage.BufferSize);

        try
        {
            for (long blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
            {
                var bytesRead = await sourceStream.ReadAsync(
                    buffer.AsMemory(0, Constants.Storage.BufferSize),
                    cancellationToken);

                if (bytesRead is 0) break;

                DeriveNonce(nonce, blockIndex, chunkNonce);

                aesGcm.Encrypt(
                    chunkNonce.AsSpan(0, Constants.Security.KeyVault.NonceSize),
                    buffer.AsSpan(0, bytesRead),
                    ciphertext.AsSpan(0, bytesRead),
                    tag.AsSpan(0, Constants.Security.KeyVault.TagSize));

                var combinedSpan = combinedBuffer.AsSpan();
                tag.AsSpan().CopyTo(combinedSpan);
                ciphertext.AsSpan(0, bytesRead).CopyTo(combinedSpan[Constants.Security.KeyVault.TagSize..]);

                await destinationStream.WriteAsync(
                    combinedBuffer.AsMemory(0, Constants.Security.KeyVault.TagSize + bytesRead),
                    cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tag, true);
            ArrayPool<byte>.Shared.Return(buffer, true);
            ArrayPool<byte>.Shared.Return(chunkNonce, true);
            ArrayPool<byte>.Shared.Return(ciphertext, true);
            ArrayPool<byte>.Shared.Return(combinedBuffer, true);
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
        ValidateSecurityOperation();

        var tag = ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.TagSize);
        var buffer = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);
        var plaintext = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);
        var chunkNonce = ArrayPool<byte>.Shared.Rent(Constants.Security.KeyVault.NonceSize);

        try
        {
            var totalLength = sourceStream.Length;
            var totalBlocks =
                (long)Math.Ceiling((double)(totalLength - Constants.Security.KeyVault.NonceSize) /
                                   (Constants.Security.KeyVault.TagSize + Constants.Storage.BufferSize));

            for (long blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
            {
                var tagRead = await sourceStream.ReadAsync(
                    tag.AsMemory(0, Constants.Security.KeyVault.TagSize),
                    cancellationToken);

                if (tagRead is 0) break;

                var bytesRead = await sourceStream.ReadAsync(
                    buffer.AsMemory(0, Constants.Storage.BufferSize),
                    cancellationToken);

                if (bytesRead is 0) break;

                DeriveNonce(nonce, blockIndex, chunkNonce);

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
            ArrayPool<byte>.Shared.Return(tag, true);
            ArrayPool<byte>.Shared.Return(buffer, true);
            ArrayPool<byte>.Shared.Return(plaintext, true);
            ArrayPool<byte>.Shared.Return(chunkNonce, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GenerateRandomKey()
    {
        ValidateSecurityOperation();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogDebugInfo(string fileId, string filePublicKey)
    {
        ValidateSecurityOperation();

        if (_logger.IsEnabled(LogLevel.Debug) is not true) return;

        _logger.LogDebug("File Id: {FileId}", fileId);
        _logger.LogDebug("File Public Key: {FilePublicKey}", filePublicKey);
    }
}