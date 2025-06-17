using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamFactoryTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamFactoryTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task Create_WithValidParameters_ShouldReturnCorrectStreamType()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "test.txt");

        await using var stream = DirectStreamFactory.Create(
            testFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Delete,
            4096,
            FileOptions.None,
            _logger);

        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public void Create_WithNullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DirectStreamFactory.Create(
                null!,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger));
    }

    [Theory]
    [InlineData(FileMode.Create)]
    [InlineData(FileMode.CreateNew)]
    [InlineData(FileMode.Open)]
    [InlineData(FileMode.OpenOrCreate)]
    [InlineData(FileMode.Truncate)]
    [InlineData(FileMode.Append)]
    public async Task Create_WithDifferentFileModes_ShouldWorkCorrectly(FileMode fileMode)
    {
        var testFilePath = Path.Combine(_testDirectoryPath, $"test_{fileMode}.txt");

        if (fileMode is FileMode.Open or FileMode.Truncate)
            await File.WriteAllTextAsync(testFilePath, "existing content");

        try
        {
            var fileAccess = fileMode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite;

            await using var stream = DirectStreamFactory.Create(
                testFilePath,
                fileMode,
                fileAccess,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger);

            Assert.NotNull(stream);

            if (stream.CanWrite is not true) return;

            var testData = "test data"u8.ToArray();

            await stream.WriteAsync(testData);
            await stream.FlushAsync();
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Theory]
    [InlineData(FileAccess.Read)]
    [InlineData(FileAccess.Write)]
    [InlineData(FileAccess.ReadWrite)]
    public async Task Create_WithDifferentFileAccess_ShouldRespectAccessModes(FileAccess fileAccess)
    {
        var testFilePath = Path.Combine(_testDirectoryPath, $"test_{fileAccess}.txt");
        await File.WriteAllTextAsync(testFilePath, "initial content");

        try
        {
            await using var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Open,
                fileAccess,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger);

            Assert.NotNull(stream);
            Assert.Equal(fileAccess.HasFlag(FileAccess.Read), stream.CanRead);
            Assert.Equal(fileAccess.HasFlag(FileAccess.Write), stream.CanWrite);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task Create_WithLogger_ShouldUseProvidedLogger()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "test_logger.txt");
        var logger = new TestLogger();

        try
        {
            await using var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                logger);

            Assert.NotNull(stream);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    private class TestLogger : ILogger
    {
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
        }
    }
}