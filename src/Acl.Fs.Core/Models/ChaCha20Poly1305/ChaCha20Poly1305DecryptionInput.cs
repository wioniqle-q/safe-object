namespace Acl.Fs.Core.Models.ChaCha20Poly1305;

public readonly record struct ChaCha20Poly1305DecryptionInput
{
    public ChaCha20Poly1305DecryptionInput(ReadOnlyMemory<byte> decryptionKey)
    {
        if (decryptionKey.IsEmpty)
            throw new ArgumentException("Decryption key cannot be empty.", nameof(decryptionKey));
        if (decryptionKey.Length is not 32)
            throw new ArgumentException("Decryption key must be exactly 32 bytes for ChaCha20Poly1305.",
                nameof(decryptionKey));
        DecryptionKey = decryptionKey;
    }

    public ReadOnlyMemory<byte> DecryptionKey { get; }
}