using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Models.AesGcm;
using Acl.Fs.Core.Pool;
using Microsoft.Extensions.Logging;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Services.Encryption.AesGcm;

internal sealed class AesEncryptionService(
    ILogger<AesEncryptionService> logger,
    IAesEncryptionBase aesEncryptionBase)
    : IAesEncryptionService
{
    private readonly IAesEncryptionBase _aesEncryptionBase =
        aesEncryptionBase ?? throw new ArgumentNullException(nameof(aesEncryptionBase));

    private readonly ILogger<AesEncryptionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task EncryptFileAsync(
        FileTransferInstruction transferInstruction,
        AesEncryptionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nonceBuffer = CryptoPool.Rent(NonceSize);

        try
        {
            RandomNumberGenerator.Fill(nonceBuffer.AsSpan(0, NonceSize));

            await _aesEncryptionBase.ExecuteEncryptionProcessAsync(
                transferInstruction, input.EncryptionKey.Span.ToArray(), nonceBuffer.AsSpan(0, NonceSize).ToArray(),
                _logger, cancellationToken);
        }
        finally
        {
            CryptoPool.Return(nonceBuffer);
            CryptographicOperations.ZeroMemory(input.EncryptionKey.Span.ToArray());
        }
    }
}