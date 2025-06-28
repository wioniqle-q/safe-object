using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.ChaCha20Poly1305;
using Acl.Fs.Core.Services.Encryption.ChaCha20Poly1305;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acl.Fs.Core.UnitTests.Services.Encryption.ChaCha20Poly1305;

public class ChaCha20Poly1305EncryptionServiceTests
{
    private const int KeySize = 32;
    private readonly Mock<IChaCha20Poly1305EncryptionBase> _chaCha20Poly1305EncryptionBaseMock;
    private readonly Mock<ILogger<ChaCha20Poly1305EncryptionService>> _loggerMock;
    private readonly ChaCha20Poly1305EncryptionService _service;

    public ChaCha20Poly1305EncryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<ChaCha20Poly1305EncryptionService>>();
        _chaCha20Poly1305EncryptionBaseMock = new Mock<IChaCha20Poly1305EncryptionBase>();
        _service = new ChaCha20Poly1305EncryptionService(_loggerMock.Object,
            _chaCha20Poly1305EncryptionBaseMock.Object);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ChaCha20Poly1305EncryptionService(null!, _chaCha20Poly1305EncryptionBaseMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullChaCha20Poly1305EncryptionBase_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ChaCha20Poly1305EncryptionService(_loggerMock.Object, null!));
        Assert.Equal("chaCha20Poly1305EncryptionBase", exception.ParamName);
    }

    [Theory]
    [InlineData(KeySize)]
    public async Task EncryptFileAsync_ValidParameters_CallsEncryptionBase(int keySize)
    {
        var key = new byte[keySize];
        RandomNumberGenerator.Fill(key);

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _chaCha20Poly1305EncryptionBaseMock.Verify(
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

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
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

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Test exception");

        _chaCha20Poly1305EncryptionBaseMock.Setup(x => x.ExecuteEncryptionProcessAsync(
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

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _chaCha20Poly1305EncryptionBaseMock.Verify(
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

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.EncryptFileAsync(transferInstruction, encryptionInput, cancellationToken);

        _chaCha20Poly1305EncryptionBaseMock.Verify(
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

        var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "test.txt"),
            GetValidPathForCurrentOs("destination", "test.txt"));
        var cancellationToken = CancellationToken.None;

        _chaCha20Poly1305EncryptionBaseMock.Setup(x => x.ExecuteEncryptionProcessAsync(
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