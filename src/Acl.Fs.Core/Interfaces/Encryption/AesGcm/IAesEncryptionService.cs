using Acl.Fs.Core.Models.AesGcm;
using FileTransferInstruction = Acl.Fs.Core.Models.FileTransferInstruction;

namespace Acl.Fs.Core.Interfaces.Encryption.AesGcm;

public interface IAesEncryptionService
{
    Task EncryptFileAsync(
        FileTransferInstruction transferInstruction,
        AesEncryptionInput input,
        CancellationToken cancellationToken);
}