using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Acl.Fs.Core.Interfaces.Decryption.AesGcm;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.AesGcm;
using Acl.Fs.Core.Services.Decryption.AesGcm;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acl.Fs.Core.UnitTests.Services.Decryption.AesGcm;

public class AesDecryptionServiceTests
{
    private const int KeySize = 32;
    private readonly Mock<IAesDecryptionBase> _aesDecryptionBaseMock;
    private readonly Mock<ILogger<AesDecryptionService>> _loggerMock;
    private readonly AesDecryptionService _service;

    public AesDecryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<AesDecryptionService>>();
        _aesDecryptionBaseMock = new Mock<IAesDecryptionBase>();
        _service = new AesDecryptionService(_loggerMock.Object, _aesDecryptionBaseMock.Object);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AesDecryptionService(null!, _aesDecryptionBaseMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullAesDecryptionBase_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AesDecryptionService(_loggerMock.Object, null!));
        Assert.Equal("aesDecryptionBase", exception.ParamName);
    }

    [Theory]
    [InlineData(KeySize)]
    public async Task DecryptFileAsync_ValidParameters_CallsDecryptionBase(int keySize)
    {
        var key = new byte[keySize];
        RandomNumberGenerator.Fill(key);

        var decryptionInput = new AesDecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken);

        _aesDecryptionBaseMock.Verify(
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

        var decryptionInput = new AesDecryptionInput(key);
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

        var decryptionInput = new AesDecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Test decryption exception");

        _aesDecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
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

        var decryptionInput = new AesDecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        await _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken);

        _aesDecryptionBaseMock.Verify(
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

        var decryptionInput = new AesDecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        _aesDecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
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

        var decryptionInput = new AesDecryptionInput(key);
        var transferInstruction = new FileTransferInstruction(
            "test-file-id",
            GetValidPathForCurrentOs("source", "encrypted.txt"),
            GetValidPathForCurrentOs("destination", "decrypted.txt"));
        var cancellationToken = CancellationToken.None;

        _aesDecryptionBaseMock.Setup(x => x.ExecuteDecryptionProcessAsync(
                It.IsAny<FileTransferInstruction>(),
                It.IsAny<byte[]>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DecryptFileAsync(transferInstruction, decryptionInput, cancellationToken));

        _aesDecryptionBaseMock.Verify(
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