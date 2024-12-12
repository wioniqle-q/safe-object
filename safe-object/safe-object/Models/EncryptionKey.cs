namespace safe_object.Models;

public sealed class EncryptionKey
{
    public string FileId { get; set; } = string.Empty;
    public string EncryptedFilePrivateKey { get; set; } = string.Empty;
}