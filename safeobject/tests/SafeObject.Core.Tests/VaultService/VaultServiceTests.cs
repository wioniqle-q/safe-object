using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace SafeObject.Core.Tests.VaultService;

public sealed class VaultServiceTests
{
    [Fact]
    public async Task StoreKey_Success()
    {
        var service = new Services.VaultService();
        var fileId = "file1";
        var privateKey = "privateKey1";
        var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var stored = await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
        Assert.NotNull(stored);
        Assert.NotEqual(Encoding.UTF8.GetBytes(privateKey), stored);
    }

    [Fact]
    public async Task RetrieveKey_ReturnsCorrectKey()
    {
        var service = new Services.VaultService();
        var fileId = "file2";
        var privateKey = "privateKey2";
        var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
        var retrieved = await service.RetrieveKeyAsync(fileId, publicMasterKey);
        Assert.Equal(privateKey, retrieved);
    }

    [Fact]
    public async Task StoreKey_Duplicate_ThrowsInvalidOperationException()
    {
        var service = new Services.VaultService();
        var fileId = "file3";
        var privateKey = "privateKey3";
        var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StoreKeyAsync(fileId, privateKey, publicMasterKey));
    }

    [Fact]
    public async Task RetrieveKey_NonexistentKey_ThrowsKeyNotFoundException()
    {
        var service = new Services.VaultService();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RetrieveKeyAsync("nonexistent", "dummyMasterKey"));
    }

    [Fact]
    public async Task StoreKey_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = new Services.VaultService();
        service.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.StoreKeyAsync("file4", "privateKey4", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    [Fact]
    public async Task RetrieveKey_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = new Services.VaultService();
        service.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.RetrieveKeyAsync("file5", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    [Fact]
    public async Task EncryptedData_IsDifferentFromPlainText()
    {
        var service = new Services.VaultService();
        var fileId = "file6";
        var privateKey = "privateKey6";
        var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var encrypted = await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
        Assert.NotEqual(Encoding.UTF8.GetBytes(privateKey), encrypted);
    }

    [Fact]
    public async Task RetrieveKey_WithIncorrectMasterKey_ThrowsCryptographicException()
    {
        var service = new Services.VaultService();
        var fileId = "file7";
        var privateKey = "privateKey7";
        var correctMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var wrongMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        await service.StoreKeyAsync(fileId, privateKey, correctMasterKey);
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            service.RetrieveKeyAsync(fileId, wrongMasterKey));
    }

    [Fact]
    public async Task ConcurrentStore_SucceedsForDifferentKeys()
    {
        var service = new Services.VaultService();
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var id = i;
            tasks.Add(Task.Run(async () =>
            {
                var fileId = $"concurrent_{id}";
                var privateKey = $"privateKey_{id}";
                var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var stored = await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
                Assert.NotNull(stored);
                Assert.NotEqual(Encoding.UTF8.GetBytes(privateKey), stored);
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentRetrieve_Succeeds()
    {
        var service = new Services.VaultService();
        var fileIds = new List<string>();
        var publicMasterKeys = new Dictionary<string, string>();

        for (var i = 0; i < 10; i++)
        {
            var fileId = $"retrieve_{i}";
            fileIds.Add(fileId);
            var privateKey = $"privateKey_{i}";
            var publicMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            publicMasterKeys[fileId] = publicMasterKey;
            await service.StoreKeyAsync(fileId, privateKey, publicMasterKey);
        }

        var tasks = fileIds.Select(fileId => Task.Run(async () =>
            {
                var result = await service.RetrieveKeyAsync(fileId, publicMasterKeys[fileId]);
                Assert.Equal($"privateKey_{fileId.Split('_')[1]}", result);
            }))
            .ToList();
        await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData(128, 16)]
    [InlineData(192, 24)]
    [InlineData(256, 32)]
    public void GenerateSystemSecurityKey_ValidKeySizes_ReturnsCorrectLength(int keySize, int expectedLength)
    {
        var method = typeof(Services.VaultService)
            .GetMethod("GenerateSystemSecurityKey", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, [keySize]);
        Assert.NotNull(result);

        var keyString = result as string;
        Assert.False(string.IsNullOrWhiteSpace(keyString));

        var keyBytes = Convert.FromBase64String(keyString);
        Assert.Equal(expectedLength, keyBytes.Length);
    }

    [Fact]
    public void GenerateSystemSecurityKey_InvalidKeySize_ThrowsArgumentOutOfRangeException()
    {
        var method = typeof(Services.VaultService)
            .GetMethod("GenerateSystemSecurityKey", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [100]));
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }
}