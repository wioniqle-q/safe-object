namespace safe_object.Interfaces;

public interface IVaultService
{
    Task<string> StoreKeyAsync(string fileId, string filePrivateKey, string filePublicMasterKey);
    Task<string> RetrieveKeyAsync(string fileId, string filePublicMasterKey);

    void Dispose();
}