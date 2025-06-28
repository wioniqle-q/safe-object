using Acl.Fs.Core.Models;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;

internal interface IChaCha20Poly1305DecryptionBase
{
    Task ExecuteDecryptionProcessAsync(
        FileTransferInstruction instruction,
        byte[] key,
        ILogger logger,
        CancellationToken cancellationToken);
}