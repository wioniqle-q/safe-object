using safe_object.Models;

namespace safe_object.Interfaces;

public interface IStorageService
{
    Task EncryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken);

    Task DecryptFileAsync(FileProcessingRequest request, string filePublicMasterKey,
        CancellationToken cancellationToken);

    void Dispose();
}