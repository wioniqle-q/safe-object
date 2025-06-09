using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamPlatformSpecificTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamPlatformSpecificTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamPlatformTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public void Create_ShouldReturnPlatformSpecificImplementation()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "platform_test.txt");

        try
        {
            using var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger);

            Assert.NotNull(stream);

            var testData = "Platform test data"u8.ToArray();
            stream.Write(testData, 0, testData.Length);
            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(testData.Length, bytesRead);
            Assert.Equal(testData, buffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task PlatformSpecificFlush_ShouldWorkOnAllPlatforms()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "platform_flush_test.txt");

        try
        {
            await using var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger);

            var testData = "Platform flush test"u8.ToArray();

            await stream.WriteAsync(testData);
            await stream.FlushAsync(CancellationToken.None);

            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = await stream.ReadAsync(buffer);

            Assert.Equal(testData.Length, bytesRead);
            Assert.Equal(testData, buffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}