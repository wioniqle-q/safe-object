using System.Security.Cryptography;

namespace SafeObject.Core.Interfaces;

public interface IHashAlgorithmProvider
{
    HMAC CreateHmac(byte[] key);
    HashAlgorithmName GetHashAlgorithmName();
    int GetHmacKeySize();
    int GetSaltSize();
}