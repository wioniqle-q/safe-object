using System.Security.Cryptography;
using Acl.Fs.Core.Models.AesGcm;

namespace Acl.Fs.Core.UnitTests.Models.AesGcm;

public sealed class AesDecryptionInputTests
{
    [Fact]
    public void Constructor_ValidKey16Bytes_CreatesInstance()
    {
        var key = new byte[16];
        RandomNumberGenerator.Fill(key);

        var input = new AesDecryptionInput(key);
        Assert.Equal(key, input.DecryptionKey);
    }

    [Fact]
    public void Constructor_ValidKey24Bytes_CreatesInstance()
    {
        var key = new byte[24];
        RandomNumberGenerator.Fill(key);

        var input = new AesDecryptionInput(key);
        Assert.Equal(key, input.DecryptionKey);
    }

    [Fact]
    public void Constructor_ValidKey32Bytes_CreatesInstance()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var input = new AesDecryptionInput(key);
        Assert.Equal(key, input.DecryptionKey);
    }

    [Fact]
    public void Constructor_EmptyKey_ThrowsArgumentException()
    {
        var emptyKey = Array.Empty<byte>();

        var exception = Assert.Throws<ArgumentException>(() => new AesDecryptionInput(emptyKey));
        Assert.Contains("Decryption key cannot be empty.", exception.Message);
        Assert.Equal("decryptionKey", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void Constructor_InvalidKeySize_ThrowsArgumentException(int keySize)
    {
        var invalidKey = new byte[keySize];
        RandomNumberGenerator.Fill(invalidKey);

        var exception = Assert.Throws<ArgumentException>(() => new AesDecryptionInput(invalidKey));
        Assert.Contains("Decryption key must be 16, 24, or 32 bytes for AES.", exception.Message);
        Assert.Equal("decryptionKey", exception.ParamName);
    }

    [Fact]
    public void AesDecryptionInput_IsRecord_SupportsValueEquality()
    {
        var key = new byte[16];
        RandomNumberGenerator.Fill(key);

        var input1 = new AesDecryptionInput(key);
        var input2 = new AesDecryptionInput(key);

        Assert.Equal(input1, input2);
        Assert.True(input1 == input2);
        Assert.Equal(input1.GetHashCode(), input2.GetHashCode());
    }

    [Fact]
    public void AesDecryptionInput_DifferentKeys_NotEqual()
    {
        var key1 = new byte[16];
        var key2 = new byte[16];

        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);

        var input1 = new AesDecryptionInput(key1);
        var input2 = new AesDecryptionInput(key2);

        Assert.NotEqual(input1, input2);
        Assert.False(input1 == input2);
    }

    [Fact]
    public void DecryptionKey_ReturnsCorrectReference()
    {
        var originalKey = new byte[32];
        RandomNumberGenerator.Fill(originalKey);

        var input = new AesDecryptionInput(originalKey);

        Assert.Equal(originalKey, input.DecryptionKey.Span.ToArray());
    }
}