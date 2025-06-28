using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;
using Acl.Fs.Core.Models.ChaCha20Poly1305;
using Microsoft.Extensions.Logging;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Services.Decryption.ChaCha20Poly1305;

internal sealed class ChaCha20Poly1305DecryptionService(
    ILogger<ChaCha20Poly1305DecryptionService> logger,
    IChaCha20Poly1305DecryptionBase chaCha20Poly1305DecryptionBase)
    : IChaCha20Poly1305DecryptionService
{
    private readonly IChaCha20Poly1305DecryptionBase _chaCha20Poly1305DecryptionBase =
        chaCha20Poly1305DecryptionBase ?? throw new ArgumentNullException(nameof(chaCha20Poly1305DecryptionBase));

    private readonly ILogger<ChaCha20Poly1305DecryptionService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task DecryptFileAsync(
        FileTransferInstruction transferInstruction,
        ChaCha20Poly1305DecryptionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _chaCha20Poly1305DecryptionBase.ExecuteDecryptionProcessAsync(
                transferInstruction,
                input.DecryptionKey.Span.ToArray(),
                _logger,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input.DecryptionKey.Span.ToArray());
        }
    }
}