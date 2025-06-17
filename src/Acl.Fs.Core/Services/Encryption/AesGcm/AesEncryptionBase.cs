using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Acl.Fs.Abstractions.Constants;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Interfaces.Factory;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Pool;
using Acl.Fs.Core.Utilities;
using Acl.Fs.Core.Utilities.AesGcm;
using Microsoft.Extensions.Logging;
using static Acl.Fs.Abstractions.Constants.StorageConstants;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Services.Encryption.AesGcm;

internal sealed class AesEncryptionBase(IAesGcmFactory aesGcmFactory)
    : IAesEncryptionBase
{
    private readonly IAesGcmFactory _aesGcmFactory =
        aesGcmFactory ?? throw new ArgumentNullException(nameof(aesGcmFactory));

    public async Task ExecuteEncryptionProcessAsync(
        FileTransferInstruction instruction,
        byte[] key,
        byte[] nonce,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = CryptoUtilities.CreateInputStream(instruction.SourcePath, logger);
        await using var destinationStream = CryptoUtilities.CreateOutputStream(instruction.DestinationPath, logger);

        using var aesGcm = _aesGcmFactory.Create(key);

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
                aesGcm,
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
        AesGcmUtilities.PrecomputeSalt(nonce, salt);

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
        System.Security.Cryptography.AesGcm aesGcm,
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
                aesGcm,
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
        System.Security.Cryptography.AesGcm aesGcm,
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

        AesGcmUtilities.DeriveNonce(salt, blockIndex, chunkNonce);

        EncryptBlock(aesGcm, buffer, ciphertext, tag, chunkNonce, alignedSize);

        await WriteEncryptedBlockAsync(destinationStream, metadataBuffer, tag, ciphertext, alignedSize,
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncryptBlock(
        System.Security.Cryptography.AesGcm aesGcm,
        byte[] buffer,
        byte[] ciphertext,
        byte[] tag,
        byte[] chunkNonce,
        int alignedSize)
    {
        aesGcm.Encrypt(
            chunkNonce.AsSpan(0, NonceSize),
            buffer.AsSpan(0, alignedSize),
            ciphertext.AsSpan(0, alignedSize),
            tag.AsSpan(0, TagSize));
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
