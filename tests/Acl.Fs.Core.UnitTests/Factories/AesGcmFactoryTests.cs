using System.Security.Cryptography;
using Acl.Fs.Core.Factories;
using Acl.Fs.Core.Resources;

namespace Acl.Fs.Core.UnitTests.Factories;

public sealed class AesGcmFactoryTests
{
    private readonly AesGcmFactory _factory = new();

    [Fact]
    public void Create_ValidKey16Bytes_ReturnsAesGcmInstance()
    {
        var key = new byte[16];
        RandomNumberGenerator.Fill(key);

        var result = _factory.Create(key);

        Assert.NotNull(result);
        Assert.IsType<AesGcm>(result);
        result.Dispose();
    }

    [Fact]
    public void Create_ValidKey24Bytes_ReturnsAesGcmInstance()
    {
        var key = new byte[24];
        RandomNumberGenerator.Fill(key);

        var result = _factory.Create(key);

        Assert.NotNull(result);
        Assert.IsType<AesGcm>(result);
        result.Dispose();
    }

    [Fact]
    public void Create_ValidKey32Bytes_ReturnsAesGcmInstance()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var result = _factory.Create(key);

        Assert.NotNull(result);
        Assert.IsType<AesGcm>(result);
        result.Dispose();
    }

    [Fact]
    public void Create_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.Create(null!));
    }

    [Fact]
    public void Create_EmptyKey_ThrowsArgumentException()
    {
        var emptyKey = Array.Empty<byte>();

        var exception = Assert.Throws<ArgumentException>(() => _factory.Create(emptyKey));
        Assert.Contains(ErrorMessages.InvalidKeySize, exception.Message);
    }

    [Fact]
    public void Create_InvalidKeySize15Bytes_ThrowsCryptographicException()
    {
        var invalidKey = new byte[15];
        RandomNumberGenerator.Fill(invalidKey);

        Assert.ThrowsAny<CryptographicException>(() => _factory.Create(invalidKey));
    }

    [Fact]
    public void Create_InvalidKeySize17Bytes_ThrowsCryptographicException()
    {
        var invalidKey = new byte[17];
        RandomNumberGenerator.Fill(invalidKey);

        Assert.ThrowsAny<CryptographicException>(() => _factory.Create(invalidKey));
    }

    [Fact]
    public void Create_InvalidKeySize33Bytes_ThrowsCryptographicException()
    {
        var invalidKey = new byte[33];
        RandomNumberGenerator.Fill(invalidKey);

        Assert.ThrowsAny<CryptographicException>(() => _factory.Create(invalidKey));
    }
}