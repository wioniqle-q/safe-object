using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        var nonce = new byte[Constants.KeyVaultConstants.NonceSize];
        RandomNumberGenerator.Fill(nonce);

        try
        {
            await _keyVaultService.StoreKeyAsync(request.FileId, filePrivateKey, filePublicMasterKey);
            await ProcessEncryptedFileAsync(request, key, nonce, cancellationToken);
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

        return new DirectStream(path, mode, access, FileShare.None, Constants.StorageConstants.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan |
            (access is FileAccess.Write ? FileOptions.WriteThrough : FileOptions.None), logger);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ProcessDecryptionAsync(byte[] key, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        var nonce = ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.NonceSize);
        try
        {
            await sourceStream.ReadExactlyAsync(nonce.AsMemory(0, Constants.KeyVaultConstants.NonceSize),
                cancellationToken);
            using var aesGcm = new AesGcm(key, Constants.KeyVaultConstants.TagSize);
            await ProcessDecryptionStreamAsync(sourceStream, destinationStream, aesGcm, nonce, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(nonce, true);
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

        var tag = ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.TagSize);
        var buffer = ArrayPool<byte>.Shared.Rent(Constants.StorageConstants.BufferSize);
        var plaintext = ArrayPool<byte>.Shared.Rent(Constants.StorageConstants.BufferSize);
        var chunkNonce = ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.NonceSize);

        try
        {
            var totalLength = sourceStream.Length;
            
            var totalBlocks =
                (long)Math.Ceiling((double)(totalLength - Constants.KeyVaultConstants.NonceSize) / 
                    Constants.KeyVaultConstants.TagSize + Constants.StorageConstants.BufferSize);
            
            var currentPrimeIndex = 0;

            for (var blockIndex = 0L; blockIndex < totalBlocks; blockIndex++)
            {
                var tagRead = await sourceStream.ReadAsync(
                    tag.AsMemory(0, Constants.KeyVaultConstants.TagSize),
                    cancellationToken);

                if (tagRead is 0) break;
                
                var bytesRead = await sourceStream.ReadAsync(
                    buffer.AsMemory(0, Constants.StorageConstants.BufferSize),
                    cancellationToken);

                if (bytesRead is 0) break;
                
                var entropyMix = (long)(blockIndex * Constants.StorageConstants.Ratio * Constants.StorageConstants.PrimeNumbers[currentPrimeIndex]);
                currentPrimeIndex = (currentPrimeIndex + 1) % Constants.StorageConstants.PrimeNumbers.Length;

                MemoryMarshal.Write(chunkNonce.AsSpan(), in MemoryMarshal.GetReference(nonce.AsSpan()));

                if (Vector.IsHardwareAccelerated)
                {
                    var nonceSpan = chunkNonce.AsSpan(0, Constants.KeyVaultConstants.NonceSize);
                    Buffer.BlockCopy(nonce, 0, chunkNonce, 0, Constants.KeyVaultConstants.NonceSize);

                    if (nonceSpan.Length >= sizeof(long))
                    {
                        var entropySpan = MemoryMarshal.Cast<long, byte>(MemoryMarshal.CreateSpan(ref entropyMix, 1));
                        var vectorNonce = MemoryMarshal.Cast<byte, Vector<byte>>(nonceSpan);
                        var vectorEntropy = MemoryMarshal.Cast<byte, Vector<byte>>(entropySpan);

                        if (vectorNonce.Length > 0 && vectorEntropy.Length > 0)
                        {
                            vectorNonce[0] = Vector.Xor(vectorNonce[0], vectorEntropy[0]);
                        }
                    }
                }
                else
                {
                    Buffer.BlockCopy(nonce, 0, chunkNonce, 0, Constants.KeyVaultConstants.NonceSize);
                    var entropyBytes = BitConverter.GetBytes(entropyMix);
                    
                    for (var i = 0; i < sizeof(long); i++)
                    {
                        chunkNonce[i] ^= entropyBytes[i];
                        chunkNonce[i] = (byte)((chunkNonce[i] << 1) ^ ((chunkNonce[i] & 0x80) == 0x80 ? 0x1B : 0x00));
                    }
                }

                aesGcm.Decrypt(
                    chunkNonce.AsSpan(0, Constants.KeyVaultConstants.NonceSize),
                    buffer.AsSpan(0, bytesRead),
                    tag.AsSpan(0, Constants.KeyVaultConstants.TagSize),
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
    private async ValueTask ProcessEncryptedFileAsync(FileProcessingRequest request, byte[] key, byte[] nonce,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        await using var sourceStream = new DirectStream(
            request.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            Constants.StorageConstants.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan,
            _logger);

        await using var destinationStream = new DirectStream(
            request.DestinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            Constants.StorageConstants.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan,
            _logger);

        await destinationStream.WriteAsync(nonce.AsMemory(), cancellationToken);

        using var aesGcm = new AesGcm(key, Constants.KeyVaultConstants.TagSize);
        
        var fileLength = sourceStream.Length;
        var totalBlocks = (long)Math.Ceiling((double)fileLength / Constants.StorageConstants.BufferSize);

        var buffer = ArrayPool<byte>.Shared.Rent(Constants.StorageConstants.BufferSize);
        var ciphertext = ArrayPool<byte>.Shared.Rent(Constants.StorageConstants.BufferSize);
        var tag = ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.TagSize);
        var chunkNonce = ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.NonceSize);
        var combinedBuffer =
            ArrayPool<byte>.Shared.Rent(Constants.KeyVaultConstants.TagSize + Constants.StorageConstants.BufferSize);

        try
        {
            long previousBlock = 0;
            long currentBlock = 1;

            for (var position = 0L; position < totalBlocks; position++)
            {
                var bytesRead = await sourceStream.ReadAsync(
                    buffer.AsMemory(0, Constants.StorageConstants.BufferSize),
                    cancellationToken);

                if (bytesRead is 0) break;
                
                var fibonacciMix = (previousBlock + currentBlock) % long.MaxValue;
                MemoryMarshal.Write(chunkNonce.AsSpan(), in MemoryMarshal.GetReference(nonce.AsSpan()));

                if (Vector.IsHardwareAccelerated)
                {
                    var nonceSpan = chunkNonce.AsSpan(0, Constants.KeyVaultConstants.NonceSize);
                    Buffer.BlockCopy(nonce, 0, chunkNonce, 0, Constants.KeyVaultConstants.NonceSize);

                    if (nonceSpan.Length >= sizeof(long))
                    {
                        var positionSpan =
                            MemoryMarshal.Cast<long, byte>(MemoryMarshal.CreateSpan(ref fibonacciMix, 1));
                        var vectorNonce = MemoryMarshal.Cast<byte, Vector<byte>>(nonceSpan);
                        var vectorPosition = MemoryMarshal.Cast<byte, Vector<byte>>(positionSpan);

                        if (vectorNonce.Length > 0 && vectorPosition.Length > 0)
                            vectorNonce[0] = Vector.Xor(vectorNonce[0], vectorPosition[0]);
                    }
                }
                else
                {
                    Buffer.BlockCopy(nonce, 0, chunkNonce, 0, Constants.KeyVaultConstants.NonceSize);
                    var mixBytes = BitConverter.GetBytes(fibonacciMix);
                    for (var i = 0; i < sizeof(long); i++)
                        chunkNonce[i] ^= mixBytes[i];
                }

                aesGcm.Encrypt(
                    chunkNonce.AsSpan(0, Constants.KeyVaultConstants.NonceSize),
                    buffer.AsSpan(0, bytesRead),
                    ciphertext.AsSpan(0, bytesRead),
                    tag.AsSpan(0, Constants.KeyVaultConstants.TagSize));

                var combinedSpan = combinedBuffer.AsSpan();
                tag.AsSpan().CopyTo(combinedSpan);
                ciphertext.AsSpan(0, bytesRead).CopyTo(combinedSpan[Constants.KeyVaultConstants.TagSize..]);

                await destinationStream.WriteAsync(
                    combinedBuffer.AsMemory(0, Constants.KeyVaultConstants.TagSize + bytesRead),
                    cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
                
                var nextBlock = (currentBlock + previousBlock) % long.MaxValue;
                previousBlock = currentBlock;
                currentBlock = nextBlock;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
            ArrayPool<byte>.Shared.Return(ciphertext, true);
            ArrayPool<byte>.Shared.Return(tag, true);
            ArrayPool<byte>.Shared.Return(chunkNonce, true);
            ArrayPool<byte>.Shared.Return(combinedBuffer, true);
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