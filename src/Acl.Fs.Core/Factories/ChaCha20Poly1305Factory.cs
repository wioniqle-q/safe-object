using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Factory;
using Acl.Fs.Core.Resources;

namespace Acl.Fs.Core.Factories;

internal sealed class ChaCha20Poly1305Factory : IChaCha20Poly1305Factory
{
    public ChaCha20Poly1305 Create(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        if (key.Length is 0)
            throw new ArgumentException(ErrorMessages.InvalidKeySize, nameof(key));

        if (key.Length is not 32)
            throw new ArgumentException("ChaCha20Poly1305 requires exactly 32 bytes key.", nameof(key));

        return new ChaCha20Poly1305(key);
    }
}