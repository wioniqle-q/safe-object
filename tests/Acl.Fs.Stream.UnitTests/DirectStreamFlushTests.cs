using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamFlushTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamFlushTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamFlushTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task FlushAsync_ShouldNotThrow()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "flush_async_test.txt");

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

            var testData = "test data"u8.ToArray();

            await stream.WriteAsync(testData);
            await stream.FlushAsync(CancellationToken.None);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task FlushAsync_WithCancellation_ShouldRespectCancellation()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "flush_cancel_test.txt");

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

            var testData = "test data"u8.ToArray();
            await stream.WriteAsync(testData);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            try
            {
                await stream.FlushAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ConcurrentFlushAsync_ShouldBeSafe()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "concurrent_flush_test.txt");

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

            var testData = "test data"u8.ToArray();
            await stream.WriteAsync(testData);

            var flushTasks = new Task[5];
            for (var i = 0; i < flushTasks.Length; i++) flushTasks[i] = stream.FlushAsync(CancellationToken.None);

            await Task.WhenAll(flushTasks);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}