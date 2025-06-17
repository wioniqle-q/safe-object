using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Decryption.AesGcm;
using Acl.Fs.Core.Models.AesGcm;
using Microsoft.Extensions.Logging;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Services.Decryption.AesGcm;

internal sealed class AesDecryptionService(
    ILogger<AesDecryptionService> logger,
    IAesDecryptionBase aesDecryptionBase)
    : IAesDecryptionService
{
    private readonly IAesDecryptionBase _aesDecryptionBase =
        aesDecryptionBase ?? throw new ArgumentNullException(nameof(aesDecryptionBase));

    private readonly ILogger<AesDecryptionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task DecryptFileAsync(
        FileTransferInstruction transferInstruction,
        AesDecryptionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _aesDecryptionBase.ExecuteDecryptionProcessAsync(
                transferInstruction, input.DecryptionKey.Span.ToArray(), _logger, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input.DecryptionKey.Span.ToArray());
        }
    }
}