using System.Security.Cryptography;
using SafeObject.Core.Helpers;
using SafeObject.Core.Interfaces;

namespace SafeObject.Core.Services.Provider;

public sealed class MacOsHashAlgorithmProvider : IHashAlgorithmProvider
{
    public HMAC CreateHmac(byte[] key)
    {
        return new HMACSHA256(key);
    }

    public HashAlgorithmName GetHashAlgorithmName()
    {
        return HashAlgorithmName.SHA256;
    }

    public int GetHmacKeySize()
    {
        return Constants.MacOs.HmacKeySize;
    }

    public int GetSaltSize()
    {
        return Constants.MacOs.SaltSize;
    }
}