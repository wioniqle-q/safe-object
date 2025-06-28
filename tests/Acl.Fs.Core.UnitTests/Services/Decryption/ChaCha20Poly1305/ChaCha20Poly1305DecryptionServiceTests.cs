using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.ChaCha20Poly1305;
using Acl.Fs.Core.Services.Decryption.ChaCha20Poly1305;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acl.Fs.Core.UnitTests.Services.Decryption.ChaCha20Poly1305;

public class ChaCha20Poly1305DecryptionServiceTests
{
    private const int KeySize = 32;
    private readonly Mock<IChaCha20Poly1305DecryptionBase> _chaCha20Poly1305DecryptionBaseMock;
    private readonly Mock<ILogger<ChaCha20Poly1305DecryptionService>> _loggerMock;
    private readonly ChaCha20Poly1305DecryptionService _service;

    public ChaCha20Poly1305DecryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<ChaCha20Poly1305DecryptionService>>();
        _chaCha20Poly1305DecryptionBaseMock = new Mock<IChaCha20Poly1305DecryptionBase>();
        _service = new ChaCha20Poly1305DecryptionService(_loggerMock.Object,
            _chaCha20Poly1305DecryptionBaseMock.Object);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ChaCha20Poly1305DecryptionService(null!, _chaCha20Poly1305DecryptionBaseMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullChaCha20Poly1305DecryptionBase_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ChaCha20Poly1305DecryptionService(_loggerMock.Object, null!));
        Assert.Equal("chaCha20Poly1305DecryptionBase", exception.ParamName);
    }

    [Theory]
    [InlineData(KeySize)]
    public async Task DecryptFileAsync_ValidParameters_CallsDecryptionBase(int keySize)
    {
        var key = new byte[keySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken);

        _chaCha20Poly1305DecryptionBaseMock.Verify(
            x => x.ExecuteDecryptionProcessAsync(
                transferInstruction,
                It.IsAny<byte[]>(),
                _loggerMock.Object,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DecryptFileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DecryptFileAsync_DecryptionBaseThrowsException_PropagatesException()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Test decryption exception");

        _chaCha20Poly1305DecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken));
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task DecryptFileAsync_ValidCall_PassesCorrectKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken);

        _chaCha20Poly1305DecryptionBaseMock.Verify(
            x => x.ExecuteDecryptionProcessAsync(
                transferInstruction,
                It.Is<byte[]>(k => k.SequenceEqual(key)),
                _loggerMock.Object,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DecryptFileAsync_CompletesSuccessfully_DoesNotThrow()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        _chaCha20Poly1305DecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken);
    }

    [Fact]
    public async Task DecryptFileAsync_ExceptionOccurs_KeyStillZeroed()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new ChaCha20Poly1305DecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        _chaCha20Poly1305DecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken));

        _chaCha20Poly1305DecryptionBaseMock.Verify(
            x => x.ExecuteDecryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string GetValidPathForCurrentOs(string directory, string filename)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $@"C:\{directory}\{filename}";

        return $"/{directory}/{filename}";
    }
}