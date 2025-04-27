using System.Security.Cryptography;
using SafeObject.Core.Helpers;
using SafeObject.Core.Interfaces;

namespace SafeObject.Core.Services.Provider;

public sealed class DefaultHashAlgorithmProvider : IHashAlgorithmProvider
{
    public HMAC CreateHmac(byte[] key)
    {
        return new HMACSHA256(key); // You may use that with HMAC3_SHA512, cause git actions does not support that.
    }

    public HashAlgorithmName GetHashAlgorithmName()
    {
        return HashAlgorithmName.SHA256; 
    }

    public int GetHmacKeySize()
    {
        return Constants.Security.KeyVault.HmacKeySize;
    }

    public int GetSaltSize()
    {
        return Constants.Security.KeyVault.SaltSize;
    }
}
