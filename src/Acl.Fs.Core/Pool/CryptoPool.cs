using System.Buffers;
using System.Security.Cryptography;

namespace Acl.Fs.Core.Pool;

internal static class CryptoPool
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;
    
    public static byte[] Rent(int minimumLength) => Pool.Rent(minimumLength);
    
    public static void Return(byte[] array, bool clearArray = true)
    {
        if (clearArray)
            CryptographicOperations.ZeroMemory(array);
        
        Pool.Return(array, clearArray);
    }
    
    public static void Return(byte[] array, int clearLength)
    {
        if (clearLength > 0)
            CryptographicOperations.ZeroMemory(array.AsSpan(0, clearLength));
        
        Pool.Return(array, clearLength > 0);
    }

    public static ArrayPool<byte> Shared => Pool;
}
