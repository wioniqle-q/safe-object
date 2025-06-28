using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Models.ChaCha20Poly1305;
using Acl.Fs.Core.Pool;
using Microsoft.Extensions.Logging;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Services.Encryption.ChaCha20Poly1305;

internal sealed class ChaCha20Poly1305EncryptionService(
    ILogger<ChaCha20Poly1305EncryptionService> logger,
    IChaCha20Poly1305EncryptionBase chaCha20Poly1305EncryptionBase)
    : IChaCha20Poly1305EncryptionService
{
    private readonly IChaCha20Poly1305EncryptionBase _chaCha20Poly1305EncryptionBase =
        chaCha20Poly1305EncryptionBase ?? throw new ArgumentNullException(nameof(chaCha20Poly1305EncryptionBase));

    private readonly ILogger<ChaCha20Poly1305EncryptionService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task EncryptFileAsync(
        FileTransferInstruction transferInstruction,
        ChaCha20Poly1305EncryptionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nonceBuffer = CryptoPool.Rent(NonceSize);

        try
        {
            RandomNumberGenerator.Fill(nonceBuffer.AsSpan(0, NonceSize));

            await _chaCha20Poly1305EncryptionBase.ExecuteEncryptionProcessAsync(
                transferInstruction,
                input.EncryptionKey.Span.ToArray(),
                nonceBuffer.AsSpan(0, NonceSize).ToArray(),
                _logger,
                cancellationToken);
        }
        finally
        {
            CryptoPool.Return(nonceBuffer);
            CryptographicOperations.ZeroMemory(input.EncryptionKey.Span.ToArray());
        }
    }
}