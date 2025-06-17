using System.Buffers;
using System.Security.Cryptography;

namespace Acl.Fs.Core.Pool;

internal static class CryptoPool
{
    public static ArrayPool<byte> Shared { get; } = ArrayPool<byte>.Shared;

    public static byte[] Rent(int minimumLength)
    {
        return Shared.Rent(minimumLength);
    }

    public static void Return(byte[] array, bool clearArray = true)
    {
        if (clearArray)
            CryptographicOperations.ZeroMemory(array);

        Shared.Return(array, clearArray);
    }

    public static void Return(byte[] array, int clearLength)
    {
        if (clearLength > 0)
            CryptographicOperations.ZeroMemory(array.AsSpan(0, clearLength));

        Shared.Return(array, clearLength > 0);
    }
}