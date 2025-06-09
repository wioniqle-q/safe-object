using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamAdditionalTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamAdditionalTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamAdditionalTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task LargeFile_WriteAndRead_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "large_file_test.txt");

        const int chunkSize = 8192;
        const int totalChunks = 100;

        var testData = new byte[chunkSize];

        for (var i = 0; i < chunkSize; i++) testData[i] = (byte)(i % 256);

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

            for (var chunk = 0; chunk < totalChunks; chunk++) await stream.WriteAsync(testData);

            await stream.FlushAsync(CancellationToken.None);

            stream.Position = 0;
            var readBuffer = new byte[chunkSize];

            for (var chunk = 0; chunk < totalChunks; chunk++)
            {
                var bytesRead = await stream.ReadAsync(readBuffer);
                Assert.Equal(chunkSize, bytesRead);
                Assert.Equal(testData, readBuffer);
            }

            Assert.Equal(chunkSize * totalChunks, stream.Length);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task MultipleStreams_SameFile_ShouldWorkWithSharing()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "shared_file_test.txt");
        var testData1 = "Stream 1 data"u8.ToArray();
        var testData2 = "Stream 2 data"u8.ToArray();

        try
        {
            await using var stream1 = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                4096,
                FileOptions.None,
                _logger);

            await using var stream2 = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                4096,
                FileOptions.None,
                _logger);

            await stream1.WriteAsync(testData1);
            await stream1.FlushAsync(CancellationToken.None);

            stream2.Position = testData1.Length;

            await stream2.WriteAsync(testData2);
            await stream2.FlushAsync(CancellationToken.None);

            stream1.Position = 0;

            var buffer1 = new byte[testData1.Length];
            var bytesRead1 = await stream1.ReadAsync(buffer1);

            var buffer2 = new byte[testData2.Length];
            var bytesRead2 = await stream1.ReadAsync(buffer2);

            Assert.Equal(testData1.Length, bytesRead1);
            Assert.Equal(testData1, buffer1);

            Assert.Equal(testData2.Length, bytesRead2);
            Assert.Equal(testData2, buffer2);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "concurrent_test.txt");

        const int taskCount = 10;
        const int dataPerTask = 1000;

        try
        {
            var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                _logger);

            try
            {
                var initialData = new byte[taskCount * dataPerTask];
                for (var i = 0; i < initialData.Length; i++) initialData[i] = (byte)(i % 256);

                await stream.WriteAsync(initialData);
                await stream.FlushAsync(CancellationToken.None);

                var readTasks = new Task<bool>[taskCount];
                for (var i = 0; i < taskCount; i++)
                {
                    var taskIndex = i;
                    readTasks[i] = Task.Run(() =>
                    {
                        var buffer = new byte[dataPerTask];
                        var offset = taskIndex * dataPerTask;

                        lock (stream)
                        {
                            stream.Position = offset;
                            var bytesRead = stream.Read(buffer, 0, buffer.Length);
                            return bytesRead == dataPerTask;
                        }
                    });
                }

                var results = await Task.WhenAll(readTasks);

                Assert.All(results, Assert.True);
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}