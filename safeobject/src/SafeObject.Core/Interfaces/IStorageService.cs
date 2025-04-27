using SafeObject.Core.Models;

namespace SafeObject.Core.Interfaces;

public interface IStorageService
{
    Task EncryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken);

    Task DecryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken);

    void Dispose();
}