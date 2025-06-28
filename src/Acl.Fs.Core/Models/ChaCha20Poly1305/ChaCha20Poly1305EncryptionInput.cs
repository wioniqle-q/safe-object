namespace Acl.Fs.Core.Models.ChaCha20Poly1305;

public readonly record struct ChaCha20Poly1305EncryptionInput
{
    public ChaCha20Poly1305EncryptionInput(ReadOnlyMemory<byte> encryptionKey)
    {
        if (encryptionKey.IsEmpty)
            throw new ArgumentException("Encryption key cannot be empty.", nameof(encryptionKey));
        if (encryptionKey.Length is not 32)
            throw new ArgumentException("Encryption key must be exactly 32 bytes for ChaCha20Poly1305.",
                nameof(encryptionKey));
        EncryptionKey = encryptionKey;
    }

    public ReadOnlyMemory<byte> EncryptionKey { get; }
}