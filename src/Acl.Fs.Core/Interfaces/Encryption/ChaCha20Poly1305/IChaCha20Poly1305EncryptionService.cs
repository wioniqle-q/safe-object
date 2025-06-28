using Acl.Fs.Core.Models.ChaCha20Poly1305;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;

public interface IChaCha20Poly1305EncryptionService
{
    Task EncryptFileAsync(
        FileTransferInstruction transferInstruction,
        ChaCha20Poly1305EncryptionInput input,
        CancellationToken cancellationToken);
}