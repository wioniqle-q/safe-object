using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace safe_object.Services;

public static class SecurityService
{
    private static readonly byte[] EncryptionKey;
    private static readonly byte[] EncryptedControlFlags;
    private static readonly byte[] PaddingBuffer;
    private static readonly int VectorSize = Vector512<byte>.Count;

    static SecurityService()
    {
        EncryptionKey = GC.AllocateArray<byte>(VectorSize, true);
        EncryptedControlFlags = GC.AllocateArray<byte>(VectorSize, true);
        PaddingBuffer = GC.AllocateArray<byte>(GetOptimalPaddingSize(), true);

        InitializeSecurityControls();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOptimalPaddingSize()
    {
        return (int)((BitOperations.RoundUpToPowerOf2((ulong)Random.Shared.Next(64, 256)) + (ulong)VectorSize - 1) &
                     (ulong)~(VectorSize - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeSecurityControls()
    {
        using var keyGenerator = RandomNumberGenerator.Create();

        Span<byte> combinedBuffer = stackalloc byte[VectorSize * 2 + PaddingBuffer.Length];
        keyGenerator.GetBytes(combinedBuffer);

        combinedBuffer[..VectorSize].CopyTo(EncryptionKey);
        combinedBuffer.Slice(VectorSize, VectorSize).CopyTo(EncryptedControlFlags);
        combinedBuffer[(VectorSize * 2)..].CopyTo(PaddingBuffer);

        if (Vector512.IsHardwareAccelerated)
        {
            var keyVector = Vector512.Create(EncryptionKey);
            var flagsVector = Vector512.Create(EncryptedControlFlags);
            (keyVector ^ flagsVector).CopyTo(EncryptedControlFlags);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            ProcessVectorized256();
        }
        else
        {
            ProcessVectorized128();
        }

        Unsafe.SkipInit(out combinedBuffer);
        ProcessPaddingBuffer();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessVectorized256()
    {
        for (var i = 0; i < EncryptedControlFlags.Length; i += Vector256<byte>.Count)
        {
            var keyVector = Vector256.LoadUnsafe(ref EncryptionKey[i]);
            var flagsVector = Vector256.LoadUnsafe(ref EncryptedControlFlags[i]);
            (keyVector ^ flagsVector).StoreUnsafe(ref EncryptedControlFlags[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessVectorized128()
    {
        for (var i = 0; i < EncryptedControlFlags.Length; i += Vector128<byte>.Count)
        {
            var keyVector = Vector128.LoadUnsafe(ref EncryptionKey[i]);
            var flagsVector = Vector128.LoadUnsafe(ref EncryptedControlFlags[i]);
            (keyVector ^ flagsVector).StoreUnsafe(ref EncryptedControlFlags[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessPaddingBuffer()
    {
        ref var paddingRef = ref MemoryMarshal.GetReference(PaddingBuffer.AsSpan());
        var length = PaddingBuffer.Length;

        if (Vector512.IsHardwareAccelerated)
            ProcessPaddingVector512(ref paddingRef, length);
        else if (Vector256.IsHardwareAccelerated)
            ProcessPaddingVector256(ref paddingRef, length);
        else
            ProcessPaddingVector128(ref paddingRef, length);

        Thread.MemoryBarrier();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessPaddingVector512(ref byte paddingRef, int length)
    {
        var entropy = GC.AllocateArray<byte>(Vector512<byte>.Count, true);

        for (var i = 0; i < length; i += Vector512<byte>.Count)
        {
            RandomNumberGenerator.Fill(entropy);
            var entropyVector = Vector512.Create(entropy);
            var dataVector = Vector512.LoadUnsafe(ref Unsafe.Add(ref paddingRef, i));
            (dataVector ^ entropyVector).StoreUnsafe(ref Unsafe.Add(ref paddingRef, i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessPaddingVector256(ref byte paddingRef, int length)
    {
        var entropy = GC.AllocateArray<byte>(Vector256<byte>.Count, true);

        for (var i = 0; i < length; i += Vector256<byte>.Count)
        {
            RandomNumberGenerator.Fill(entropy);
            var entropyVector = Vector256.Create(entropy);
            var dataVector = Vector256.LoadUnsafe(ref Unsafe.Add(ref paddingRef, i));
            (dataVector ^ entropyVector).StoreUnsafe(ref Unsafe.Add(ref paddingRef, i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessPaddingVector128(ref byte paddingRef, int length)
    {
        var entropy = GC.AllocateArray<byte>(Vector128<byte>.Count, true);

        for (var i = 0; i < length; i += Vector128<byte>.Count)
        {
            RandomNumberGenerator.Fill(entropy);
            var entropyVector = Vector128.Create(entropy);
            var dataVector = Vector128.LoadUnsafe(ref Unsafe.Add(ref paddingRef, i));
            (dataVector ^ entropyVector).StoreUnsafe(ref Unsafe.Add(ref paddingRef, i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ValidateOperation()
    {
        var keySpan = EncryptionKey.AsSpan();
        var flagsSpan = EncryptedControlFlags.AsSpan();

        var check1 = (byte)(Unsafe.ReadUnaligned<byte>(ref MemoryMarshal.GetReference(keySpan)) ^
                            Unsafe.ReadUnaligned<byte>(ref MemoryMarshal.GetReference(flagsSpan)));

        ProcessPaddingBuffer();

        var check2 = (byte)(Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref MemoryMarshal.GetReference(keySpan), 15)) ^
                            Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref MemoryMarshal.GetReference(flagsSpan), 15)));

        Span<byte> actual = [check1, check2];
        Span<byte> expected =
        [
            (byte)(flagsSpan[0] ^ keySpan[0]),
            (byte)(flagsSpan[15] ^ keySpan[15])
        ];

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}