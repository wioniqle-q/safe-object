using System.Buffers;
using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Models.AesGcm;
using Microsoft.Extensions.Logging;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Services.Encryption.AesGcm;

internal sealed class AesEncryptionService(
    ILogger<AesEncryptionService> logger,
    IAesEncryptionBase aesEncryptionBase)
    : IAesEncryptionService
{
    private static readonly ArrayPool<byte> NoncePool = ArrayPool<byte>.Shared;

    private readonly IAesEncryptionBase _aesEncryptionBase =
        aesEncryptionBase ?? throw new ArgumentNullException(nameof(aesEncryptionBase));

    private readonly ILogger<AesEncryptionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task EncryptFileAsync(
        FileTransferInstruction transferInstruction,
        AesEncryptionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nonceBuffer = NoncePool.Rent(NonceSize);

        try
        {
            RandomNumberGenerator.Fill(nonceBuffer.AsSpan(0, NonceSize));

            var nonce = new byte[NonceSize];
            nonceBuffer.AsSpan(0, NonceSize).CopyTo(nonce);

            await _aesEncryptionBase.ExecuteEncryptionProcessAsync(
                transferInstruction, input.EncryptionKey.Span.ToArray(), nonce, _logger, cancellationToken);
        }
        finally
        {
            NoncePool.Return(nonceBuffer, true);
            CryptographicOperations.ZeroMemory(input.EncryptionKey.Span.ToArray());
        }
    }
}