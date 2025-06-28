using System.Security.Cryptography;
using Acl.Fs.Core.Utilities;
using static Acl.Fs.Abstractions.Constants.KeyVaultConstants;

namespace Acl.Fs.Core.UnitTests.Utilities;

public sealed class CryptographicUtilitiesTests
{
    [Fact]
    public void PrecomputeSalt_ValidInputs_GeneratesSalt()
    {
        var originalNonce = new byte[32];
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(originalNonce);

        CryptographicUtilities.PrecomputeSalt(originalNonce, salt);

        Assert.NotNull(salt);
        Assert.Equal(SaltSize, salt.Length);
        Assert.False(IsAllZeros(salt));
    }

    [Fact]
    public void PrecomputeSalt_SameInputs_GeneratesSameSalt()
    {
        var originalNonce = new byte[32];
        for (var i = 0; i < originalNonce.Length; i++)
            originalNonce[i] = (byte)(i % 256);

        var salt1 = new byte[SaltSize];
        var salt2 = new byte[SaltSize];

        CryptographicUtilities.PrecomputeSalt(originalNonce, salt1);
        CryptographicUtilities.PrecomputeSalt(originalNonce, salt2);

        Assert.Equal(salt1, salt2);
    }

    [Fact]
    public void PrecomputeSalt_DifferentInputs_GeneratesDifferentSalts()
    {
        var originalNonce1 = new byte[32];
        var originalNonce2 = new byte[32];
        new Random(42).NextBytes(originalNonce1);
        new Random(84).NextBytes(originalNonce2);

        var salt1 = new byte[SaltSize];
        var salt2 = new byte[SaltSize];

        CryptographicUtilities.PrecomputeSalt(originalNonce1, salt1);
        CryptographicUtilities.PrecomputeSalt(originalNonce2, salt2);


        Assert.NotEqual(salt1, salt2);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(64)]
    public void PrecomputeSalt_VariousNonceSizes_WorksCorrectly(int nonceSize)
    {
        var originalNonce = new byte[nonceSize];
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(originalNonce);

        var exception = Record.Exception(() => CryptographicUtilities.PrecomputeSalt(originalNonce, salt));
        Assert.Null(exception);
        Assert.False(IsAllZeros(salt));
    }

    [Fact]
    public void DeriveNonce_ValidInputs_GeneratesNonce()
    {
        const long blockIndex = 12345;

        var salt = new byte[SaltSize];
        var outputNonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(salt);

        CryptographicUtilities.DeriveNonce(salt, blockIndex, outputNonce);

        Assert.NotNull(outputNonce);
        Assert.Equal(NonceSize, outputNonce.Length);
        Assert.False(IsAllZeros(outputNonce));
    }

    [Fact]
    public void DeriveNonce_SameInputs_GeneratesSameNonce()
    {
        var salt = new byte[SaltSize];
        for (var i = 0; i < salt.Length; i++)
            salt[i] = (byte)(i % 256);

        const long blockIndex = 12345;
        var outputNonce1 = new byte[NonceSize];
        var outputNonce2 = new byte[NonceSize];


        CryptographicUtilities.DeriveNonce(salt, blockIndex, outputNonce1);
        CryptographicUtilities.DeriveNonce(salt, blockIndex, outputNonce2);


        Assert.Equal(outputNonce1, outputNonce2);
    }

    [Fact]
    public void DeriveNonce_DifferentBlockIndex_GeneratesDifferentNonces()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var outputNonce1 = new byte[NonceSize];
        var outputNonce2 = new byte[NonceSize];

        CryptographicUtilities.DeriveNonce(salt, 1, outputNonce1);
        CryptographicUtilities.DeriveNonce(salt, 2, outputNonce2);

        Assert.NotEqual(outputNonce1, outputNonce2);
    }

    [Fact]
    public void DeriveNonce_DifferentSalts_GeneratesDifferentNonces()
    {
        var salt1 = new byte[SaltSize];
        var salt2 = new byte[SaltSize];
        new Random(42).NextBytes(salt1);
        new Random(84).NextBytes(salt2);

        const long blockIndex = 12345;
        var outputNonce1 = new byte[NonceSize];
        var outputNonce2 = new byte[NonceSize];


        CryptographicUtilities.DeriveNonce(salt1, blockIndex, outputNonce1);
        CryptographicUtilities.DeriveNonce(salt2, blockIndex, outputNonce2);


        Assert.NotEqual(outputNonce1, outputNonce2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(long.MaxValue)]
    public void DeriveNonce_VariousBlockIndices_WorksCorrectly(long blockIndex)
    {
        var salt = new byte[SaltSize];
        var outputNonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(salt);

        var exception = Record.Exception(() => CryptographicUtilities.DeriveNonce(salt, blockIndex, outputNonce));
        Assert.Null(exception);
        Assert.False(IsAllZeros(outputNonce));
    }

    [Fact]
    public void DeriveNonce_SequentialBlockIndices_GeneratesUniqueNonces()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var nonces = new List<byte[]>();
        const int numNonces = 10;


        for (long i = 0; i < numNonces; i++)
        {
            var outputNonce = new byte[NonceSize];
            CryptographicUtilities.DeriveNonce(salt, i, outputNonce);
            nonces.Add(outputNonce);
        }


        for (var i = 0; i < numNonces; i++)
        for (var j = i + 1; j < numNonces; j++)
            Assert.NotEqual(nonces[i], nonces[j]);
    }

    [Fact]
    public void PrecomputeSalt_EmptyNonce_WorksWithEmptyInput()
    {
        var emptyNonce = Array.Empty<byte>();
        var salt = new byte[SaltSize];

        var exception = Record.Exception(() => CryptographicUtilities.PrecomputeSalt(emptyNonce, salt));

        Assert.Null(exception);
        Assert.False(IsAllZeros(salt));
    }

    [Fact]
    public void DeriveNonce_EmptySalt_WorksWithEmptyInput()
    {
        var emptySalt = Array.Empty<byte>();
        var outputNonce = new byte[NonceSize];

        var exception = Record.Exception(() => CryptographicUtilities.DeriveNonce(emptySalt, 1, outputNonce));

        Assert.True(exception is null or CryptographicException);
    }

    [Fact]
    public void PrecomputeSalt_WrongSaltSize_StillWorks()
    {
        var originalNonce = new byte[32];
        var wrongSizeSalt = new byte[16];
        RandomNumberGenerator.Fill(originalNonce);

        var exception = Record.Exception(() => CryptographicUtilities.PrecomputeSalt(originalNonce, wrongSizeSalt));

        Assert.NotNull(exception);
        Assert.IsType<CryptographicException>(exception);
    }

    [Fact]
    public void DeriveNonce_WrongNonceSize_ThrowsException()
    {
        var salt = new byte[SaltSize];
        var wrongSizeNonce = new byte[8];
        RandomNumberGenerator.Fill(salt);

        var exception = Record.Exception(() => CryptographicUtilities.DeriveNonce(salt, 1, wrongSizeNonce));
        Assert.NotNull(exception);
        Assert.IsType<CryptographicException>(exception);
    }

    [Fact]
    public void AesGcmUtilities_WorksWithRealWorldScenario()
    {
        var masterKey = new byte[32];
        RandomNumberGenerator.Fill(masterKey);

        var salt = new byte[SaltSize];
        CryptographicUtilities.PrecomputeSalt(masterKey, salt);

        var nonce1 = new byte[NonceSize];
        var nonce2 = new byte[NonceSize];
        CryptographicUtilities.DeriveNonce(salt, 0, nonce1);
        CryptographicUtilities.DeriveNonce(salt, 1, nonce2);

        Assert.False(IsAllZeros(salt));
        Assert.False(IsAllZeros(nonce1));
        Assert.False(IsAllZeros(nonce2));
        Assert.NotEqual(nonce1, nonce2);

        var salt2 = new byte[SaltSize];
        CryptographicUtilities.PrecomputeSalt(masterKey, salt2);
        Assert.Equal(salt, salt2);
    }

    private static bool IsAllZeros(byte[] array)
    {
        return array.All(b => b == 0);
    }
}