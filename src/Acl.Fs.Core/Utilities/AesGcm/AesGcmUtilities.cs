using System.Buffers.Binary;
using System.Security.Cryptography;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.Utilities.AesGcm;

internal static class AesGcmUtilities
{
    internal static void PrecomputeSalt(byte[] originalNonce, byte[] salt)
    {
        Span<byte> input = stackalloc byte[8];
        try
        {
            BinaryPrimitives.WriteInt64LittleEndian(input, 0L);

            if (IsRunningOnGitHubActions)
            {
                using var hmac = new HMACSHA256(originalNonce);
                if (hmac.TryComputeHash(input, salt, out var bytesWritten) is not true ||
                    bytesWritten != SaltSize)
                    throw new CryptographicException("Failed to derive salt.");
            }
            else
            {
                using var hmac = new HMACSHA3_512(originalNonce);
                if (hmac.TryComputeHash(input, salt, out var bytesWritten) is not true ||
                    bytesWritten != SaltSize)
                    throw new CryptographicException("Failed to derive salt.");
            }
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("Failed to derive salt.", ex);
        }
        finally
        {
            input.Clear();
        }
    }

    internal static void DeriveNonce(byte[] salt, long blockIndex, byte[] outputNonce)
    {
        Span<byte> blockIndexBytes = stackalloc byte[sizeof(long)];
        Span<byte> prk = stackalloc byte[HmacKeySize];
        Span<byte> info = stackalloc byte[sizeof(long) + NonceContext.Length];
        Span<byte> okm = stackalloc byte[NonceSize];

        try
        {
            BinaryPrimitives.WriteInt64LittleEndian(blockIndexBytes, blockIndex);

            if (IsRunningOnGitHubActions)
            {
                using (var hmac = new HMACSHA256(salt))
                {
                    if (hmac.TryComputeHash(blockIndexBytes, prk, out var bytesWritten) is not true ||
                        bytesWritten != HmacKeySize)
                        throw new CryptographicException("HMAC computation failed.");
                }

                blockIndexBytes.CopyTo(info);
                NonceContext.CopyTo(info[sizeof(long)..]);

                HKDF.Expand(HashAlgorithmName.SHA256, prk, okm, info);
            }
            else
            {
                using (var hmac = new HMACSHA3_512(salt))
                {
                    if (hmac.TryComputeHash(blockIndexBytes, prk, out var bytesWritten) is not true ||
                        bytesWritten != HmacKeySize)
                        throw new CryptographicException("HMAC computation failed.");
                }

                blockIndexBytes.CopyTo(info);
                NonceContext.CopyTo(info[sizeof(long)..]);

                HKDF.Expand(HashAlgorithmName.SHA3_512, prk, okm, info);
            }

            okm.CopyTo(outputNonce.AsSpan(0, NonceSize));
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("Failed to derive nonce.", ex);
        }
        finally
        {
            prk.Clear();
            okm.Clear();
            info.Clear();
            blockIndexBytes.Clear();
        }
    }
}