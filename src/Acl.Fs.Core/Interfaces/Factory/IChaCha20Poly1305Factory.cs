using System.Security.Cryptography;

namespace Acl.Fs.Core.Interfaces.Factory;

internal interface IChaCha20Poly1305Factory
{
    ChaCha20Poly1305 Create(byte[] key);
}