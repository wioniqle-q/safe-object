using Acl.Fs.Core.Models;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Core.Interfaces.Encryption.AesGcm;

internal interface IAesEncryptionBase
{
    Task ExecuteEncryptionProcessAsync(
        FileTransferInstruction instruction,
        byte[] key,
        byte[] nonce,
        ILogger logger,
        CancellationToken cancellationToken);
}