using Acl.Fs.Core.Models.AesGcm;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Interfaces.Decryption.AesGcm;

public interface IAesDecryptionService
{
    Task DecryptFileAsync(
        FileTransferInstruction transferInstruction,
        AesDecryptionInput input,
        CancellationToken cancellationToken);
}