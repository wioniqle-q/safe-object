using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using safe_object.Interfaces;
using safe_object.Models;
using Sodium;

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
    
        var filePrivateKey = Convert.ToBase64String(SecretBox.GenerateKey());
        var key = Convert.FromBase64String(filePrivateKey);
        var nonce = SecretBox.GenerateNonce();
    
        try
        {
            await _keyVaultService.StoreKeyAsync(request.FileId, filePrivateKey, filePublicMasterKey);
        
            await using var sourceStream = CreateFileStream(request.SourcePath, FileMode.Open, FileAccess.Read, _logger);
            await using var destinationStream =
                CreateFileStream(request.DestinationPath, FileMode.Create, FileAccess.Write, _logger);
            
            await ProcessEncryptedFileAsync(key, nonce, sourceStream, destinationStream, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(nonce);
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

   
    private static async ValueTask ProcessEncryptedFileAsync(byte[] key, byte[] nonce, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();
    
        await destinationStream.WriteAsync(nonce, cancellationToken);
        await destinationStream.FlushAsync(cancellationToken);
    
        var buffer = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);
        var ciphertextBuffer = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize + 16);
    
        try
        {
            while (true)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, Constants.Storage.BufferSize),
                    cancellationToken);
                if (bytesRead is 0) break;
            
                var chunkNonce = SecretBox.GenerateNonce();
                var encrypted = SecretBox.Create(buffer.AsSpan(0, bytesRead).ToArray(), chunkNonce, key);
                
                var lengthBytes = BitConverter.GetBytes(encrypted.Length);

                await destinationStream.WriteAsync(chunkNonce, cancellationToken);
                await destinationStream.WriteAsync(lengthBytes, cancellationToken);
                await destinationStream.WriteAsync(encrypted, cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
            ArrayPool<byte>.Shared.Return(ciphertextBuffer, true);
        }
    }

    private static async Task ProcessDecryptionAsync(byte[] key, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        var nonce = new byte[SecretBox.GenerateNonce().Length];
        await sourceStream.ReadExactlyAsync(nonce, cancellationToken);

        var buffer = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize + 16); 
        var plaintext = ArrayPool<byte>.Shared.Rent(Constants.Storage.BufferSize);

        try
        {
            while (true)
            {
                var chunkNonce = new byte[SecretBox.GenerateNonce().Length];
                
                var nonceRead = await sourceStream.ReadAsync(chunkNonce, cancellationToken);
                if (nonceRead is 0) break;

                var lengthBytes = new byte[4];
                
                var lengthRead = await sourceStream.ReadAsync(lengthBytes, cancellationToken);
                if (lengthRead is 0) break;
                if (lengthRead is not 4)
                    throw new CryptographicException("Failed to read ciphertext length.");

                var ciphertextLength = BitConverter.ToInt32(lengthBytes, 0);
                if (ciphertextLength > buffer.Length)
                    throw new CryptographicException(
                        $"Ciphertext length {ciphertextLength} exceeds buffer size {buffer.Length}.");

                await sourceStream.ReadExactlyAsync(buffer.AsMemory(0, ciphertextLength), cancellationToken);

                var decrypted = SecretBox.Open(buffer.AsSpan(0, ciphertextLength).ToArray(), chunkNonce, key);
                await destinationStream.WriteAsync(decrypted, cancellationToken);
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
            ArrayPool<byte>.Shared.Return(plaintext, true);
        }
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