using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.AesGcm;
using Acl.Fs.Core.Services.Encryption.AesGcm;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acl.Fs.Core.UnitTests.Services.Encryption.AesGcm;

public class AesEncryptionServiceTests
{
    private const int KeySize = 32;
    private readonly Mock<IAesEncryptionBase> _aesEncryptionBaseMock;
    private readonly Mock<ILogger<AesEncryptionService>> _loggerMock;
    private readonly AesEncryptionService _service;

    public AesEncryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<AesEncryptionService>>();
        _aesEncryptionBaseMock = new Mock<IAesEncryptionBase>();
        _service = new AesEncryptionService(_loggerMock.Object, _aesEncryptionBaseMock.Object);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AesEncryptionService(null!, _aesEncryptionBaseMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullAesEncryptionBase_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AesEncryptionService(_loggerMock.Object, null!));
        Assert.Equal("aesEncryptionBase", exception.ParamName);
    }

    [Theory]
    [InlineData(KeySize)]
    public async Task EncryptFileAsync_ValidParameters_CallsEncryptionBase(int keySize)
    {
        var key = new byte[keySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _aesEncryptionBaseMock.Verify(
            x => x.ExecuteEncryptionProcessAsync(
                transferInstruction,
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                _loggerMock.Object,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task EncryptFileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task EncryptFileAsync_EncryptionBaseThrowsException_PropagatesException()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Test exception");

        _aesEncryptionBaseMock.Setup(x => x.ExecuteEncryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken));
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task EncryptFileAsync_ValidCall_PassesCorrectNonceSize()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _aesEncryptionBaseMock.Verify(
            x => x.ExecuteEncryptionProcessAsync(
                transferInstruction,
                It.IsAny<byte[]>(),
                It.Is<byte[]>(nonce => nonce.Length == 12),
                _loggerMock.Object,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task EncryptFileAsync_ValidCall_PassesCorrectKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _aesEncryptionBaseMock.Verify(
            x => x.ExecuteEncryptionProcessAsync(
                transferInstruction,
                It.Is<byte[]>(k => k.SequenceEqual(key)),
                It.IsAny<byte[]>(),
                _loggerMock.Object,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task EncryptFileAsync_CompletesSuccessfully_DoesNotThrow()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new AesEncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        _aesEncryptionBaseMock.Setup(x => x.ExecuteEncryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);
    }

    private static string GetValidPathForCurrentOs(string directory, string filename)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $@"C:\{directory}\{filename}";

        return $"/{directory}/{filename}";
    }
}