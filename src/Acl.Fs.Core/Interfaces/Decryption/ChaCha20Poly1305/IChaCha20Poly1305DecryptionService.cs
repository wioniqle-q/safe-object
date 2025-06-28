using Acl.Fs.Core.Models.ChaCha20Poly1305;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;

public interface IChaCha20Poly1305DecryptionService
{
    Task DecryptFileAsync(
        FileTransferInstruction transferInstruction,
        ChaCha20Poly1305DecryptionInput input,
        CancellationToken cancellationToken);
}