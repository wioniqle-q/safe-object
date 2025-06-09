namespace Acl.Fs.Core.Models.AesGcm;

public readonly record struct AesDecryptionInput
{
    public AesDecryptionInput(ReadOnlyMemory<byte> decryptionKey)
    {
        if (decryptionKey.IsEmpty)
            throw new ArgumentException("Decryption key cannot be empty.", nameof(decryptionKey));
        if (decryptionKey.Length is not 16 and not 24 and not 32)
            throw new ArgumentException("Decryption key must be 16, 24, or 32 bytes for AES.", nameof(decryptionKey));
        DecryptionKey = decryptionKey;
    }

    public ReadOnlyMemory<byte> DecryptionKey { get; }
}