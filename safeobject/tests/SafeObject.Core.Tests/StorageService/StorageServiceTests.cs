using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using SafeObject.Core.Helpers;
using SafeObject.Core.Interfaces;
using SafeObject.Core.Models;
using Xunit;

namespace SafeObject.Core.Tests.StorageService;

public static class ExceptionExtensions
{
    public static void As<T>() where T : Exception
    {
    }
}

public sealed class StorageServiceTests : IDisposable
{
    private const int NonceSize = Constants.Security.KeyVault.NonceSize;
    private const int SaltSize = Constants.Security.KeyVault.SaltSize;

    private readonly MethodInfo _deriveNonceMethod;
    private readonly MethodInfo _precomputeSaltMethod;

    private readonly string _tempDir;
    private readonly ConcurrentDictionary<string, byte[]> _vaultDictionary = new();

    internal StorageServiceTests(Type storageServiceType)
    {
        _precomputeSaltMethod =
            storageServiceType.GetMethod("PrecomputeSalt", BindingFlags.NonPublic | BindingFlags.Static)!;
        if (_precomputeSaltMethod is null)
            throw new Exception("Could not find PrecomputeSalt method.");

        _deriveNonceMethod = storageServiceType.GetMethod("DeriveNonce", BindingFlags.NonPublic | BindingFlags.Static)!;
        if (_deriveNonceMethod is null)
            throw new Exception("Could not find DeriveNonce method.");

        _tempDir = Path.Combine(Path.GetTempPath(), "SafeObjectTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public StorageServiceTests() : this(typeof(Services.StorageService))
    {
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void DeriveNonce_WithNullSalt_ThrowsCryptographicException()
    {
        var blockIndex = 0L;
        var outputNonce = new byte[Constants.Security.KeyVault.NonceSize];

        var exception = Assert.Throws<TargetInvocationException>(() =>
                _deriveNonceMethod.Invoke(null, [null, blockIndex, outputNonce]))
            .InnerException;
        Assert.IsType<CryptographicException>(exception);
        Assert.Equal("Failed to derive nonce.", exception.Message);
    }

    [Fact]
    public void DeriveNonce_FillsOutputNonce_WithValidParameters()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        var blockIndex = 0L;
        var outputNonce = new byte[NonceSize];
        Array.Clear(outputNonce, 0, outputNonce.Length);

        _deriveNonceMethod.Invoke(null, [salt, blockIndex, outputNonce]);

        Assert.Contains(outputNonce, b => b != 0);
    }

    [Fact]
    public void DeriveNonce_WithTooSmallOutputNonce_ThrowsCryptographicException()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        var blockIndex = 0L;

        var outputNonce = new byte[NonceSize - 1];

        var exception = Assert.Throws<TargetInvocationException>(() =>
                _deriveNonceMethod.Invoke(null, [salt, blockIndex, outputNonce]))
            .InnerException;
        Assert.IsType<CryptographicException>(exception);
        Assert.Equal("Failed to derive nonce.", exception.Message);
    }

    [Fact]
    public void PrecomputeSalt_SetsNonZeroSalt_WhenValidParameters()
    {
        var nonceSize = Constants.Security.KeyVault.NonceSize;
        var saltSize = Constants.Security.KeyVault.SaltSize;
        var originalNonce = new byte[nonceSize];

        RandomNumberGenerator.Fill(originalNonce);
        var salt = new byte[saltSize];
        Array.Clear(salt, 0, salt.Length);

        _precomputeSaltMethod.Invoke(null, [originalNonce, salt]);

        Assert.Contains(salt, b => b != 0);
    }

    [Fact]
    public async Task EncryptAndDecryptFileAsync_ShouldRecoverOriginalContent()
    {
        var fileId = Guid.NewGuid().ToString();
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted.dat");
        var decryptedFile = Path.Combine(_tempDir, "decrypted.txt");
        var originalContent = "Hello, World!";
        await File.WriteAllTextAsync(sourceFile, originalContent);

        var fileProcessingRequestEnc = new FileProcessingRequest(fileId, sourceFile, encryptedFile);
        var fileProcessingRequestDec = new FileProcessingRequest(fileId, encryptedFile, decryptedFile);

        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string id, string key, string _) =>
            {
                var keyBytes = Encoding.UTF8.GetBytes(key);
                _vaultDictionary[id] = keyBytes;
                return keyBytes;
            });
        vaultServiceMock.Setup(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string id, string _) =>
                _vaultDictionary.TryGetValue(id, out var key) ? Encoding.UTF8.GetString(key) : string.Empty);

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        var cancellationToken = CancellationToken.None;

        await storageService.EncryptFileAsync(fileProcessingRequestEnc, "dummyPublicKey", cancellationToken);
        await storageService.DecryptFileAsync(fileProcessingRequestDec, "dummyPublicKey", cancellationToken);

        var decryptedContent = await File.ReadAllTextAsync(decryptedFile, cancellationToken);
        Assert.Equal(originalContent, decryptedContent);
    }

    [Fact]
    public void Dispose_ShouldDisposeVaultService()
    {
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.As<IDisposable>().Setup(v => v.Dispose());
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        storageService.Dispose();

        vaultServiceMock.As<IDisposable>().Verify(v => v.Dispose(), Times.Once);
    }

    [Fact]
    public async Task EncryptFileAsync_ShouldThrow_OperationCanceledException()
    {
        var fileId = Guid.NewGuid().ToString();
        var sourceFile = Path.Combine(_tempDir, "source_cancel.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted_cancel.dat");
        await File.WriteAllTextAsync(sourceFile, "TestContent");

        var request = new FileProcessingRequest(fileId, sourceFile, encryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[16]);
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            storageService.EncryptFileAsync(request, "dummyPublicKey", cts.Token));
    }

    [Fact]
    public async Task DecryptFileAsync_ShouldThrow_OperationCanceledException()
    {
        var fileId = Guid.NewGuid().ToString();
        var encryptedFile = Path.Combine(_tempDir, "encrypted_cancel_dec.dat");
        var decryptedFile = Path.Combine(_tempDir, "decrypted_cancel.txt");
        await File.WriteAllTextAsync(encryptedFile, "DummyEncryptedContent");

        var request = new FileProcessingRequest(fileId, encryptedFile, decryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Convert.ToBase64String(new byte[16]));
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            storageService.DecryptFileAsync(request, "dummyPublicKey", cts.Token));
    }

    [Fact]
    public async Task EncryptFileAsync_WithNonexistentSource_ShouldThrow_FileNotFoundException()
    {
        var fileId = Guid.NewGuid().ToString();
        var nonExistentSource = Path.Combine(_tempDir, "nonexistent.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted_nonexistent.dat");
        var request = new FileProcessingRequest(fileId, nonExistentSource, encryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[16]);
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            storageService.EncryptFileAsync(request, "dummyPublicKey", CancellationToken.None));
    }

    [Fact]
    public async Task DecryptFileAsync_WithInvalidKey_ShouldThrow_FormatException()
    {
        var fileId = Guid.NewGuid().ToString();
        var encryptedFile = Path.Combine(_tempDir, "encrypted_invalid_key.dat");
        var decryptedFile = Path.Combine(_tempDir, "decrypted_invalid_key.txt");

        await File.WriteAllTextAsync(encryptedFile, "DummyContent");

        var request = new FileProcessingRequest(fileId, encryptedFile, decryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();

        vaultServiceMock.Setup(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("invalid_base64");
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        await Assert.ThrowsAsync<FormatException>(() =>
            storageService.DecryptFileAsync(request, "dummyPublicKey", CancellationToken.None));
    }

    [Fact]
    public async Task EncryptFileAsync_AfterDisposed_ShouldThrow_ObjectDisposedException()
    {
        var fileId = Guid.NewGuid().ToString();
        var sourceFile = Path.Combine(_tempDir, "source_disposed.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted_disposed.dat");
        await File.WriteAllTextAsync(sourceFile, "Content");
        var request = new FileProcessingRequest(fileId, sourceFile, encryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[16]);
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);
        storageService.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            storageService.EncryptFileAsync(request, "dummyPublicKey", CancellationToken.None));
    }

    [Fact]
    public async Task DecryptFileAsync_AfterDisposed_ShouldThrow_ObjectDisposedException()
    {
        var fileId = Guid.NewGuid().ToString();
        var encryptedFile = Path.Combine(_tempDir, "encrypted_disposed_dec.dat");
        var decryptedFile = Path.Combine(_tempDir, "decrypted_disposed.txt");
        await File.WriteAllTextAsync(encryptedFile, "DummyContent");
        var request = new FileProcessingRequest(fileId, encryptedFile, decryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Convert.ToBase64String(new byte[16]));
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);
        storageService.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            storageService.DecryptFileAsync(request, "dummyPublicKey", CancellationToken.None));
    }

    [Fact]
    public async Task EncryptFileAsync_ShouldCall_StoreKeyAsync_Once()
    {
        var fileId = Guid.NewGuid().ToString();
        var sourceFile = Path.Combine(_tempDir, "source_vault.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted_vault.dat");
        await File.WriteAllTextAsync(sourceFile, "VaultTestContent");
        var request = new FileProcessingRequest(fileId, sourceFile, encryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[16]);
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        await storageService.EncryptFileAsync(request, "dummyPublicKey", CancellationToken.None);

        vaultServiceMock.Verify(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task DecryptFileAsync_ShouldCall_RetrieveKeyAsync_Once()
    {
        var fileId = Guid.NewGuid().ToString();
        var encryptedFile = Path.Combine(_tempDir, "encrypted_vault_dec.dat");
        var decryptedFile = Path.Combine(_tempDir, "decrypted_vault.txt");
        await File.WriteAllTextAsync(encryptedFile, "DummyContent");
        var request = new FileProcessingRequest(fileId, encryptedFile, decryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Convert.ToBase64String(new byte[16]));
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        await storageService.DecryptFileAsync(request, "dummyPublicKey", CancellationToken.None);

        vaultServiceMock.Verify(v => v.RetrieveKeyAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EncryptFileAsync_ShouldCreateEncryptedFile_WithValidContent()
    {
        var fileId = Guid.NewGuid().ToString();
        var sourceFile = Path.Combine(_tempDir, "source_encrypted.txt");
        var encryptedFile = Path.Combine(_tempDir, "encrypted_output.dat");
        var content = "Some content for encryption.";
        await File.WriteAllTextAsync(sourceFile, content);
        var request = new FileProcessingRequest(fileId, sourceFile, encryptedFile);
        var vaultServiceMock = new Mock<IVaultService>();
        vaultServiceMock.Setup(v => v.StoreKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[16]);
        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var storageService = new Services.StorageService(loggerMock.Object, vaultServiceMock.Object);

        await storageService.EncryptFileAsync(request, "dummyPublicKey", CancellationToken.None);

        Assert.True(File.Exists(encryptedFile));
        var fileInfo = new FileInfo(encryptedFile);

        Assert.True(fileInfo.Length > 0);
    }
}