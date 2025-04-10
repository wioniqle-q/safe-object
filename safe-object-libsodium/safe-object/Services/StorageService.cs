using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Security;
using Microsoft.Extensions.Logging;
using safe_object.Interfaces;
using safe_object.Models;
using Sodium;
using static safe_object.Constants;

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

        await _keyVaultService.StoreKeyAsync(request.FileId, filePrivateKey, filePublicMasterKey);
        await ProcessEncryptedFileAsync(request, key, cancellationToken);
    }

    public async Task DecryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateSecurityOperation();
        LogDebugInfo(request.FileId, filePublicMasterKey);

        var filePrivateKey = await _keyVaultService.RetrieveKeyAsync(request.FileId, filePublicMasterKey);
        var key = Convert.FromBase64String(filePrivateKey);

        await using var sourceStream = CreateFileStream(request.SourcePath, FileMode.Open, FileAccess.Read, logger);
        await using var destinationStream =
            CreateFileStream(request.DestinationPath, FileMode.Create, FileAccess.Write, logger);
        await ProcessDecryptionAsync(key, sourceStream, destinationStream, cancellationToken);
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
        return new DirectStream(path, mode, access, FileShare.None, Storage.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan |
            (access is FileAccess.Write ? FileOptions.WriteThrough : FileOptions.None), logger);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask ProcessEncryptedFileAsync(FileProcessingRequest request, byte[] key,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        await using var sourceStream = CreateFileStream(
            request.SourcePath, FileMode.Open, FileAccess.Read, _logger);
        await using var destinationStream = CreateFileStream(
            request.DestinationPath, FileMode.Create, FileAccess.Write, _logger);

        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;

        var readingTask = FillPipeAsync(sourceStream, writer, cancellationToken);
        var writingTask = EncryptAndWritePipeAsync(reader, destinationStream, key, cancellationToken);

        await Task.WhenAll(readingTask, writingTask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task FillPipeAsync(Stream source, PipeWriter writer, CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        const int chunkSize = Storage.BufferSize;
        while (true)
        {
            var buffer = writer.GetMemory(chunkSize);
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead is 0) break;

            writer.Advance(bytesRead);
            await writer.FlushAsync(cancellationToken);
        }

        await writer.CompleteAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task EncryptAndWritePipeAsync(PipeReader reader, Stream destination, byte[] key,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted) break;

            foreach (var segment in buffer)
                for (var offset = 0; offset < segment.Length; offset += Storage.BufferSize)
                {
                    var chunkSize = Math.Min(Storage.BufferSize, segment.Length - offset);

                    var nonce = SecretAeadXChaCha20Poly1305.GenerateNonce();
                    var plaintextChunk = ArrayPool<byte>.Shared.Rent(chunkSize);

                    try
                    {
                        segment.Slice(offset, chunkSize).CopyTo(plaintextChunk.AsMemory(0, chunkSize));
                        var ciphertext = SecretAeadXChaCha20Poly1305.Encrypt(
                            plaintextChunk.AsSpan(0, chunkSize).ToArray(),
                            nonce,
                            key);

                        var sizeBytes = BitConverter.GetBytes(ciphertext.Length);
                        await destination.WriteAsync(sizeBytes, cancellationToken);
                        await destination.WriteAsync(nonce, cancellationToken);
                        await destination.WriteAsync(ciphertext, cancellationToken);

                        await destination.FlushAsync(cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(plaintextChunk);
                    }
                }

            reader.AdvanceTo(buffer.End);
        }

        await reader.CompleteAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ProcessDecryptionAsync(byte[] key, Stream sourceStream, Stream destinationStream,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;

        var readingTask = FillPipeAsync(sourceStream, writer, cancellationToken);
        var writingTask = DecryptAndWritePipeAsync(reader, destinationStream, key, cancellationToken);

        await Task.WhenAll(readingTask, writingTask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task DecryptAndWritePipeAsync(PipeReader reader, Stream destination, byte[] key,
        CancellationToken cancellationToken)
    {
        ValidateSecurityOperation();

        var lengthBuffer = new byte[sizeof(int)];
        var nonceBuffer = new byte[Security.KeyVault.NonceSize];

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted) break;

            var position = buffer.Start;
            var remaining = buffer.Length;

            while (remaining >= sizeof(int) + Security.KeyVault.NonceSize + 1)
            {
                buffer.Slice(position, sizeof(int)).CopyTo(lengthBuffer);
                var ciphertextLength = BitConverter.ToInt32(lengthBuffer, 0);

                if (remaining < sizeof(int) + Security.KeyVault.NonceSize + ciphertextLength) break;

                buffer.Slice(buffer.GetPosition(sizeof(int), position), Security.KeyVault.NonceSize)
                    .CopyTo(nonceBuffer);

                var ciphertextBuffer = ArrayPool<byte>.Shared.Rent(ciphertextLength);
                try
                {
                    buffer.Slice(buffer.GetPosition(sizeof(int) + Security.KeyVault.NonceSize, position),
                            ciphertextLength)
                        .CopyTo(ciphertextBuffer.AsSpan(0, ciphertextLength));

                    var plaintext = SecretAeadXChaCha20Poly1305.Decrypt(
                        ciphertextBuffer.AsSpan(0, ciphertextLength).ToArray(),
                        nonceBuffer,
                        key);

                    await destination.WriteAsync(plaintext, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(ciphertextBuffer);
                }

                position = buffer.GetPosition(sizeof(int) + Security.KeyVault.NonceSize + ciphertextLength,
                    position);
                remaining -= sizeof(int) + Security.KeyVault.NonceSize + ciphertextLength;
            }

            reader.AdvanceTo(position, buffer.End);

            if (result.IsCompleted is not true) continue;

            break;
        }

        await reader.CompleteAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GenerateRandomKey()
    {
        ValidateSecurityOperation();
        return SodiumCore.GetRandomBytes(Security.KeyVault.KeySize);
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