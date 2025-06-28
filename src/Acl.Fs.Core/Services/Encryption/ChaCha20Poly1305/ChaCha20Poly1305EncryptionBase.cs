using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Acl.Fs.Abstractions.Constants;
using Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Interfaces.Factory;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Pool;
using Acl.Fs.Core.Utilities;
using Microsoft.Extensions.Logging;
using static Acl.Fs.Abstractions.Constants.StorageConstants;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Services.Encryption.ChaCha20Poly1305;

internal sealed class ChaCha20Poly1305EncryptionBase(IChaCha20Poly1305Factory chaCha20Poly1305Factory)
    : IChaCha20Poly1305EncryptionBase
{
    private readonly IChaCha20Poly1305Factory _chaCha20Poly1305Factory =
        chaCha20Poly1305Factory ?? throw new ArgumentNullException(nameof(chaCha20Poly1305Factory));

    public async Task ExecuteEncryptionProcessAsync(
        FileTransferInstruction instruction,
        byte[] key,
        byte[] nonce,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = CryptoUtilities.CreateInputStream(instruction.SourcePath, logger);
        await using var destinationStream = CryptoUtilities.CreateOutputStream(instruction.DestinationPath, logger);

        using var chaCha20Poly1305 = _chaCha20Poly1305Factory.Create(key);

        var buffer = CryptoPool.Rent(BufferSize);
        var ciphertext = CryptoPool.Rent(BufferSize);
        var metadataBuffer = CryptoPool.Rent(SectorSize);
        var tag = CryptoPool.Rent(TagSize);
        var chunkNonce = CryptoPool.Rent(NonceSize);
        var salt = CryptoPool.Rent(SaltSize);

        try
        {
            PrepareMetadata(nonce, sourceStream.Length, salt, metadataBuffer);
            await WriteHeaderAsync(destinationStream, metadataBuffer, cancellationToken);

            await ProcessFileBlocksAsync(
                sourceStream,
                destinationStream,
                chaCha20Poly1305,
                buffer,
                ciphertext,
                metadataBuffer,
                tag,
                chunkNonce,
                salt,
                cancellationToken);
        }
        finally
        {
            CryptoPool.Return(buffer);
            CryptoPool.Return(ciphertext);
            CryptoPool.Return(metadataBuffer);
            CryptoPool.Return(tag);
            CryptoPool.Return(chunkNonce);
            CryptoPool.Return(salt);
        }
    }

    private static void PrepareMetadata(byte[] nonce, long originalSize, byte[] salt, byte[] metadataBuffer)
    {
        CryptographicUtilities.PrecomputeSalt(nonce, salt);

        metadataBuffer.AsSpan(0, VersionConstants.HeaderSize).Clear();

        metadataBuffer[0] = VersionConstants.CurrentMajorVersion;
        metadataBuffer[1] = VersionConstants.CurrentMinorVersion;

        var offset = VersionConstants.VersionHeaderSize;

        nonce.AsSpan(0, NonceSize)
            .CopyTo(metadataBuffer.AsSpan(offset));

        offset += NonceSize;

        BinaryPrimitives.WriteInt64LittleEndian(
            metadataBuffer.AsSpan(offset),
            originalSize);

        offset += sizeof(long);

        salt.CopyTo(metadataBuffer.AsSpan(offset));
    }

    private static async Task WriteHeaderAsync(
        System.IO.Stream destinationStream,
        byte[] metadataBuffer,
        CancellationToken cancellationToken)
    {
        await destinationStream.WriteAsync(
            metadataBuffer.AsMemory(0, VersionConstants.HeaderSize),
            cancellationToken);
    }

    private static async Task ProcessFileBlocksAsync(
        System.IO.Stream sourceStream,
        System.IO.Stream destinationStream,
        System.Security.Cryptography.ChaCha20Poly1305 chaCha20Poly1305,
        byte[] buffer,
        byte[] ciphertext,
        byte[] metadataBuffer,
        byte[] tag,
        byte[] chunkNonce,
        byte[] salt,
        CancellationToken cancellationToken)
    {
        var totalBlocks = (sourceStream.Length + BufferSize - 1) / BufferSize;

        for (long blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            var bytesRead = await sourceStream.ReadAsync(
                buffer.AsMemory(0, BufferSize),
                cancellationToken);

            if (bytesRead is 0) break;

            await EncryptAndWriteBlockAsync(
                destinationStream,
                chaCha20Poly1305,
                buffer,
                ciphertext,
                metadataBuffer,
                tag,
                chunkNonce,
                salt,
                bytesRead,
                blockIndex,
                totalBlocks,
                cancellationToken);
        }
    }

    private static async Task EncryptAndWriteBlockAsync(
        System.IO.Stream destinationStream,
        System.Security.Cryptography.ChaCha20Poly1305 chaCha20Poly1305,
        byte[] buffer,
        byte[] ciphertext,
        byte[] metadataBuffer,
        byte[] tag,
        byte[] chunkNonce,
        byte[] salt,
        int bytesRead,
        long blockIndex,
        long totalBlocks,
        CancellationToken cancellationToken)
    {
        var isLastBlock = blockIndex == totalBlocks - 1 || bytesRead < BufferSize;
        var alignedSize = CryptoUtilities.CalculateAlignedSize(bytesRead, isLastBlock);

        if (bytesRead < alignedSize) buffer.AsSpan(bytesRead, alignedSize - bytesRead).Clear();

        CryptographicUtilities.DeriveNonce(salt, blockIndex, chunkNonce);

        EncryptBlock(chaCha20Poly1305, buffer, ciphertext, tag, chunkNonce, alignedSize, blockIndex, salt);

        await WriteEncryptedBlockAsync(destinationStream, metadataBuffer, tag, ciphertext, alignedSize,
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(
        System.Security.Cryptography.ChaCha20Poly1305 chaCha20Poly1305,
        byte[] buffer,
        byte[] ciphertext,
        byte[] tag,
        byte[] chunkNonce,
        int alignedSize,
        long blockIndex,
        byte[] salt)
    {
        Span<byte> associatedData = stackalloc byte[64 + sizeof(long) + sizeof(int)];

        salt.AsSpan(0, Math.Min(64, salt.Length)).CopyTo(associatedData);

        BinaryPrimitives.WriteInt64LittleEndian(associatedData[64..], blockIndex);
        BinaryPrimitives.WriteInt32LittleEndian(associatedData[72..], alignedSize);

        chaCha20Poly1305.Encrypt(
            chunkNonce.AsSpan(0, NonceSize),
            buffer.AsSpan(0, alignedSize),
            ciphertext.AsSpan(0, alignedSize),
            tag.AsSpan(0, TagSize),
            associatedData);
    }

    private static async Task WriteEncryptedBlockAsync(
        System.IO.Stream destinationStream,
        byte[] metadataBuffer,
        byte[] tag,
        byte[] ciphertext,
        int alignedSize,
        CancellationToken cancellationToken)
    {
        metadataBuffer.AsSpan(0, SectorSize).Clear();

        tag.AsSpan(0, TagSize)
            .CopyTo(metadataBuffer);

        await destinationStream.WriteAsync(
            metadataBuffer.AsMemory(0, SectorSize),
            cancellationToken);

        await destinationStream.WriteAsync(
            ciphertext.AsMemory(0, alignedSize),
            cancellationToken);
    }
}