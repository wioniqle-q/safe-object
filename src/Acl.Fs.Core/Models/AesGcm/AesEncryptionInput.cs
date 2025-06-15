namespace Acl.Fs.Core.Models.AesGcm;

public readonly record struct AesEncryptionInput
{
    public AesEncryptionInput(ReadOnlyMemory<byte> encryptionKey)
    {
        if (encryptionKey.IsEmpty)
            throw new ArgumentException("Encryption key cannot be empty.", nameof(encryptionKey));
        if (encryptionKey.Length is not 16 and not 24 and not 32)
            throw new ArgumentException("Encryption key must be 16, 24, or 32 bytes for AES.", nameof(encryptionKey));
        EncryptionKey = encryptionKey;
    }

    public ReadOnlyMemory<byte> EncryptionKey { get; }
}