using System.Security.Cryptography;
using Acl.Fs.Core.Models.ChaCha20Poly1305;

namespace Acl.Fs.Core.UnitTests.Models.ChaCha20Poly1305;

public sealed class ChaCha20Poly1305DecryptionInputTests
{
    [Fact]
    public void Constructor_ValidKey32Bytes_CreatesInstance()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var input = new ChaCha20Poly1305DecryptionInput(key);
        Assert.Equal(key, input.DecryptionKey.Span.ToArray());
    }

    [Fact]
    public void Constructor_EmptyKey_ThrowsArgumentException()
    {
        var emptyKey = Array.Empty<byte>();

        var exception = Assert.Throws<ArgumentException>(() => new ChaCha20Poly1305DecryptionInput(emptyKey));
        Assert.Contains("Decryption key cannot be empty.", exception.Message);
        Assert.Equal("decryptionKey", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void Constructor_InvalidKeySize_ThrowsArgumentException(int keySize)
    {
        var invalidKey = new byte[keySize];
        RandomNumberGenerator.Fill(invalidKey);

        var exception = Assert.Throws<ArgumentException>(() => new ChaCha20Poly1305DecryptionInput(invalidKey));
        Assert.Contains("Decryption key must be exactly 32 bytes for ChaCha20Poly1305.", exception.Message);
        Assert.Equal("decryptionKey", exception.ParamName);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptionInput_IsRecord_SupportsValueEquality()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var input1 = new ChaCha20Poly1305DecryptionInput(key);
        var input2 = new ChaCha20Poly1305DecryptionInput(key);

        Assert.Equal(input1, input2);
        Assert.True(input1 == input2);
        Assert.Equal(input1.GetHashCode(), input2.GetHashCode());
    }

    [Fact]
    public void ChaCha20Poly1305DecryptionInput_DifferentKeys_NotEqual()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];

        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);

        var input1 = new ChaCha20Poly1305DecryptionInput(key1);
        var input2 = new ChaCha20Poly1305DecryptionInput(key2);

        Assert.NotEqual(input1, input2);
        Assert.False(input1 == input2);
    }

    [Fact]
    public void DecryptionKey_ReturnsCorrectReference()
    {
        var originalKey = new byte[32];
        RandomNumberGenerator.Fill(originalKey);

        var input = new ChaCha20Poly1305DecryptionInput(originalKey);

        Assert.Equal(originalKey, input.DecryptionKey.Span.ToArray());
    }
}