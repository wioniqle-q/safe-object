using System.Reflection;
using Moq;
using SafeObject.Core.Models;
using Xunit;

namespace SafeObject.Core.Tests.Models;

public class EncryptionKeyTests
{
    private const string ValidFileId = "validFileId";
    private static readonly byte[] ValidEncryptedKey = [1, 2, 3, 4];

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateInstance()
    {
        var key = new EncryptionKey(ValidFileId, ValidEncryptedKey);
        Assert.Equal(ValidFileId, key.FileId);
        Assert.Equal(ValidEncryptedKey, key.EncryptedFilePrivateKey);
    }

    [Fact]
    public void Constructor_NullFileId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EncryptionKey(null!, ValidEncryptedKey));
    }

    [Fact]
    public void Constructor_EmptyFileId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EncryptionKey(string.Empty, ValidEncryptedKey));
    }

    [Fact]
    public void Constructor_WhitespaceFileId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EncryptionKey("   ", ValidEncryptedKey));
    }

    [Fact]
    public void Constructor_EmptyEncryptedKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EncryptionKey(ValidFileId, []));
    }

    [Fact]
    public void Record_ValueEquality_SameParameters_AreEqual()
    {
        var key1 = new EncryptionKey(ValidFileId, ValidEncryptedKey);
        var key2 = new EncryptionKey(ValidFileId, ValidEncryptedKey);
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Record_ValueEquality_DifferentFileId_AreNotEqual()
    {
        var key1 = new EncryptionKey(ValidFileId, ValidEncryptedKey);
        var key2 = new EncryptionKey("differentFileId", ValidEncryptedKey);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Record_ValueEquality_DifferentEncryptedKey_AreNotEqual()
    {
        var key1 = new EncryptionKey(ValidFileId, ValidEncryptedKey);
        var differentEncryptedKey = new byte[] { 9, 8, 7, 6 };
        var key2 = new EncryptionKey(ValidFileId, differentEncryptedKey);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Moq_DummyService_Test()
    {
        var mock = new Mock<IDummyService>();
        mock.Setup(s => s.GetValue()).Returns(42);
        Assert.Equal(42, mock.Object.GetValue());
        mock.Verify(s => s.GetValue(), Times.Once);
    }

    private static MethodInfo GetValidateByteArrayMethod()
    {
        var method = typeof(EncryptionKey).GetMethod("ValidateByteArray", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method;
    }

    [Fact]
    public void ValidateByteArray_WithNonByteArray_ReturnsFalse()
    {
        var method = GetValidateByteArrayMethod();
        var result = (bool)method.Invoke(null, ["not a byte array"])!;
        Assert.False(result);
    }

    [Fact]
    public void ValidateByteArray_WithEmptyByteArray_ReturnsFalse()
    {
        var method = GetValidateByteArrayMethod();
        var result = (bool)method.Invoke(null, [Array.Empty<byte>()])!;
        Assert.False(result);
    }

    [Fact]
    public void ValidateByteArray_WithNonEmptyByteArray_ReturnsTrue()
    {
        var method = GetValidateByteArrayMethod();
        var result = (bool)method.Invoke(null, [new byte[] { 1, 2, 3 }])!;
        Assert.True(result);
    }
}

public interface IDummyService
{
    int GetValue();
}