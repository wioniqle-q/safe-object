namespace SafeObject.Core.Interfaces;

public interface IVaultService
{
    Task<byte[]> StoreKeyAsync(string fileId, string filePrivateKey, string filePublicMasterKey);
    Task<string> RetrieveKeyAsync(string fileId, string filePublicMasterKey);

    void Dispose();
}