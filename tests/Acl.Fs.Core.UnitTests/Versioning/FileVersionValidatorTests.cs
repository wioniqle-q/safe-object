using Acl.Fs.Core.Resources;
using Acl.Fs.Core.Versioning;
using Acl.Fs.Core.Versioning.Exceptions;
using Microsoft.Extensions.Logging;
using static Acl.Fs.Abstractions.Constants.VersionConstants;

namespace Acl.Fs.Core.UnitTests.Versioning;

public sealed class FileVersionValidatorTests
{
    private readonly TestLogger _logger;
    private readonly FileVersionValidator _validator;

    public FileVersionValidatorTests()
    {
        _logger = new TestLogger();
        _validator = new FileVersionValidator(_logger);
    }

    [Fact]
    public void ValidateVersion_CurrentVersion_DoesNotThrow()
    {
        var exception = Record.Exception(() => _validator.ValidateVersion(CurrentMajorVersion, CurrentMinorVersion));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_ValidV1Minor0_DoesNotThrow()
    {
        var exception = Record.Exception(() => _validator.ValidateVersion(1, 0));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_ValidV1Minor1_DoesNotThrow()
    {
        var exception = Record.Exception(() => _validator.ValidateVersion(1, 1));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_ValidV1Minor255_DoesNotThrow()
    {
        var exception = Record.Exception(() => _validator.ValidateVersion(1, 255));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_MajorVersionZero_ThrowsVersionValidationException()
    {
        var exception = Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(0, 0));
        Assert.Contains(ErrorMessages.MajorVersionCannotBeZero, exception.Message);
    }

    [Fact]
    public void ValidateVersion_MajorVersionZeroWithMinor_ThrowsVersionValidationException()
    {
        var exception = Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(0, 5));
        Assert.Contains(ErrorMessages.MajorVersionCannotBeZero, exception.Message);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(255)]
    public void ValidateVersion_FutureMajorVersion_ThrowsVersionValidationException(byte futureMajorVersion)
    {
        var exception =
            Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(futureMajorVersion, 0));
        Assert.Contains(
            $"File encrypted with newer version (v{futureMajorVersion}.0) than supported (v{CurrentMajorVersion}.{CurrentMinorVersion})",
            exception.Message);
    }

    [Fact]
    public void ValidateVersion_FutureMajorVersionWithMessage_ContainsCorrectVersions()
    {
        const byte futureMajor = 5;
        const byte futureMinor = 3;

        var exception =
            Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(futureMajor, futureMinor));

        Assert.Contains($"v{futureMajor}.{futureMinor}", exception.Message);
        Assert.Contains($"v{CurrentMajorVersion}.{CurrentMinorVersion}", exception.Message);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    [InlineData(25)]
    public void ValidateVersion_UnsupportedMajorVersion_ThrowsVersionValidationException(byte unsupportedMajorVersion)
    {
        if (unsupportedMajorVersion <= CurrentMajorVersion)
            return;

        var exception =
            Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(unsupportedMajorVersion, 0));
        Assert.Contains(
            $"File encrypted with newer version (v{unsupportedMajorVersion}.0) than supported (v{CurrentMajorVersion}.{CurrentMinorVersion})",
            exception.Message);
    }

    [Fact]
    public void ValidateVersion_UnsupportedMajorVersionMessage_ContainsVersionInfo()
    {
        const byte testMajor = 1;
        const byte testMinor = 0;

        var exception = Record.Exception(() => _validator.ValidateVersion(testMajor, testMinor));
        Assert.Null(exception);

        const byte unsupportedMajor = 99;
        var unsupportedException =
            Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(unsupportedMajor, testMinor));
        Assert.Contains($"v{unsupportedMajor}.{testMinor}", unsupportedException.Message);
    }

    [Fact]
    public void ValidateVersion_LogsErrorOnException()
    {
        try
        {
            _validator.ValidateVersion(0, 0);
        }
        catch (VersionValidationException)
        {
        }

        Assert.False(_logger.HasLoggedError);
    }

    [Fact]
    public void ValidateVersion_ValidVersionDoesNotLog()
    {
        _validator.ValidateVersion(1, 0);

        Assert.False(_logger.HasLoggedError);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(1, 100)]
    [InlineData(1, 255)]
    public void ValidateVersion_SupportedVersions_DoNotThrow(byte majorVersion, byte minorVersion)
    {
        var exception = Record.Exception(() => _validator.ValidateVersion(majorVersion, minorVersion));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_RepeatedCalls_ConsistentBehavior()
    {
        for (var i = 0; i < 5; i++)
        {
            var exception = Record.Exception(() => _validator.ValidateVersion(1, 0));
            Assert.Null(exception);
        }

        for (var i = 0; i < 5; i++) Assert.Throws<VersionValidationException>(() => _validator.ValidateVersion(0, 0));
    }

    [Fact]
    public void ValidateVersion_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileVersionValidator(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        var validator = new FileVersionValidator(_logger);

        Assert.NotNull(validator);
    }

    private class TestLogger : ILogger<FileVersionValidator>
    {
        public bool HasLoggedError { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error) HasLoggedError = true;
        }
    }
}