using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamReadWriteTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamReadWriteTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamReadWriteTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task WriteAsync_AndReadAsync_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "async_test.txt");
        var testData = "Hello, World!"u8.ToArray();

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

            await stream.WriteAsync(testData);
            await stream.FlushAsync(CancellationToken.None);

            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory());

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
    public async Task WriteAsync_Memory_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "memory_test.txt");
        var testData = "Hello, Memory!"u8.ToArray();

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

            await stream.WriteAsync(new ReadOnlyMemory<byte>(testData));
            await stream.FlushAsync(CancellationToken.None);

            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = await stream.ReadAsync(new Memory<byte>(buffer));

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
    public async Task Seek_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "seek_test.txt");
        var testData = "0123456789"u8.ToArray();

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

            await stream.WriteAsync(testData);

            var pos1 = stream.Seek(5, SeekOrigin.Begin);
            Assert.Equal(5, pos1);
            Assert.Equal(5, stream.Position);

            var pos2 = stream.Seek(-2, SeekOrigin.Current);
            Assert.Equal(3, pos2);
            Assert.Equal(3, stream.Position);

            var pos3 = stream.Seek(-3, SeekOrigin.End);
            Assert.Equal(7, pos3);
            Assert.Equal(7, stream.Position);

            var buffer = new byte[2];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 2));

            Assert.Equal(2, bytesRead);
            Assert.Equal("78"u8.ToArray(), buffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task SetLength_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "setlength_test.txt");
        var testData = "Hello, World!"u8.ToArray();

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

            await stream.WriteAsync(testData);
            Assert.Equal(testData.Length, stream.Length);

            stream.SetLength(5);
            Assert.Equal(5, stream.Length);

            stream.Position = 0;

            var buffer = new byte[10];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory());

            Assert.Equal(5, bytesRead);
            Assert.Equal("Hello"u8.ToArray(), buffer[..5]);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task Stream_Properties_ShouldReflectCorrectValues()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "properties_test.txt");

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

            Assert.True(stream.CanRead);
            Assert.True(stream.CanWrite);
            Assert.True(stream.CanSeek);

            Assert.Equal(0, stream.Length);
            Assert.Equal(0, stream.Position);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void Write_SynchronousWrite_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_write_test.txt");
        var testData = "Hello, Sync Write!"u8.ToArray();

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
    public void Read_SynchronousRead_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_read_test.txt");
        var testData = "Hello, Sync Read!"u8.ToArray();

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
    public async Task ReadAsync_WithArrayAndCancellation_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "read_async_array_test.txt");
        var testData = "Hello, ReadAsync Array!"u8.ToArray();

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

            await stream.WriteAsync(testData);
            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

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
    public async Task WriteAsync_WithArrayAndCancellation_ShouldWorkCorrectly()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "write_async_array_test.txt");
        var testData = "Hello, WriteAsync Array!"u8.ToArray();

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

            await stream.WriteAsync(testData, 0, testData.Length, CancellationToken.None);
            stream.Position = 0;

            var buffer = new byte[testData.Length];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory());

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
    public async Task ReadAsync_WithCancellation_ShouldThrowWhenCancelled()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "read_async_cancel_test.txt");
        var testData = "Hello, Cancel Test!"u8.ToArray();

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

            await stream.WriteAsync(testData);
            stream.Position = 0;

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var buffer = new byte[testData.Length];

            try
            {
                await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
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
    public async Task WriteAsync_WithCancellation_ShouldThrowWhenCancelled()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "write_async_cancel_test.txt");
        var testData = "Hello, Cancel Test!"u8.ToArray();

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

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            try
            {
                await stream.WriteAsync(testData, 0, testData.Length, cts.Token);
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
    public void Write_SyncWrite_TestsCompletedAndNonCompletedPaths()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_write_paths_test.txt");
        var testData = "Test Data"u8.ToArray();

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

            stream.Write(testData, 0, testData.Length);

            var largeData = new byte[16384];
            stream.Write(largeData, 0, largeData.Length);

            for (var i = 0; i < 10; i++) stream.Write(new byte[10], 0, 10);

            Assert.True(stream.Length >= testData.Length + largeData.Length + 100);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void Write_SyncWrite_ForceAsyncPath()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_write_async_path_test.txt");

        try
        {
            using var stream = DirectStreamFactory.Create(
                testFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                1024,
                FileOptions.None,
                _logger);

            var largeData = new byte[32768];
            for (var i = 0; i < largeData.Length; i++) largeData[i] = (byte)(i % 256);

            stream.Write(largeData, 0, largeData.Length);

            stream.Position = 0;
            var readBuffer = new byte[largeData.Length];
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

            Assert.Equal(largeData.Length, bytesRead);
            Assert.Equal(largeData, readBuffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void Read_SyncRead_TestsCompletedAndNonCompletedPaths()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_read_paths_test.txt");
        var testData = new byte[2048];
        for (var i = 0; i < testData.Length; i++) testData[i] = (byte)(i % 256);

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

            stream.Write(testData, 0, testData.Length);
            stream.Position = 0;

            var buffer1 = new byte[100];
            var bytesRead1 = stream.Read(buffer1, 0, buffer1.Length);
            Assert.Equal(100, bytesRead1);

            var buffer2 = new byte[1024];
            var bytesRead2 = stream.Read(buffer2, 0, buffer2.Length);
            Assert.Equal(1024, bytesRead2);

            var buffer3 = new byte[1];
            var bytesRead3 = stream.Read(buffer3, 0, buffer3.Length);
            Assert.Equal(1, bytesRead3);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void Flush_SynchronousFlush_ShouldNotThrow()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "sync_flush_test.txt");

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

            var testData = "test data"u8.ToArray();
            stream.Write(testData, 0, testData.Length);

            stream.Flush();
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void DisposedStream_ShouldThrowObjectDisposedException()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "disposed_test.txt");

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

            stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[10], 0, 10));
            Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[10], 0, 10));
            Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<ObjectDisposedException>(() => stream.SetLength(100));
            Assert.Throws<ObjectDisposedException>(() => stream.Flush());
            Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task DisposedStreamAsync_ShouldThrowObjectDisposedException()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "disposed_async_test.txt");

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

            await stream.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.ReadAsync(new byte[10], 0, 10));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.WriteAsync(new byte[10], 0, 10));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.FlushAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                stream.ReadAsync(new Memory<byte>(new byte[10])).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                stream.WriteAsync(new ReadOnlyMemory<byte>(new byte[10])).AsTask());
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void DoubleDispose_ShouldNotThrow()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "double_dispose_test.txt");

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

            stream.Dispose();
            stream.Dispose();
            stream.Dispose();
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task DoubleDisposeAsync_ShouldNotThrow()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "double_dispose_async_test.txt");

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

            await stream.DisposeAsync();
            await stream.DisposeAsync();
            await stream.DisposeAsync();
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeSafe()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "concurrent_test.txt");
        var testData = new byte[1024];
        Random.Shared.NextBytes(testData);

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

            await using (stream)
            {
                await stream.WriteAsync(testData);

                var tasks = new List<Task>();
                var exceptions = new List<Exception>();
                using var semaphore = new SemaphoreSlim(1, 1);

                for (var i = 0; i < 10; i++)
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var buffer = new byte[100];
                                var pos = Random.Shared.Next(0, testData.Length - 100);
                                stream.Position = pos;
                                await stream.ReadAsync(buffer.AsMemory());
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));

                for (var i = 0; i < 5; i++)
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var buffer = new byte[50];
                                Random.Shared.NextBytes(buffer);
                                var pos = Random.Shared.Next(0, testData.Length - 50);
                                stream.Position = pos;
                                await stream.WriteAsync(buffer);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));

                for (var i = 0; i < 5; i++)
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var pos = Random.Shared.Next(0, testData.Length);
                                stream.Seek(pos, SeekOrigin.Begin);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));

                await Task.WhenAll(tasks);

                Assert.Empty(exceptions);
            }
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void PositionSynchronization_AfterSetLength_ShouldBeConsistent()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "position_sync_setlength_test.txt");

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

            var testData = new byte[1000];
            Random.Shared.NextBytes(testData);
            stream.Write(testData, 0, testData.Length);

            Assert.Equal(1000, stream.Position);

            stream.SetLength(500);

            Assert.True(stream.Position <= 500);
            Assert.Equal(500, stream.Length);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task PositionSynchronization_AfterFlush_ShouldBeConsistent()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "position_sync_flush_test.txt");

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

            var testData = new byte[100];
            Random.Shared.NextBytes(testData);
            await stream.WriteAsync(testData);

            var positionBeforeFlush = stream.Position;
            await stream.FlushAsync();
            var positionAfterFlush = stream.Position;

            Assert.True(positionAfterFlush >= positionBeforeFlush,
                $"Position after flush ({positionAfterFlush}) should be >= position before flush ({positionBeforeFlush})");
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void SeekOperations_ShouldHandleAllOrigins()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "seek_origins_test.txt");

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

            var testData = new byte[1000];
            stream.Write(testData, 0, testData.Length);

            var pos1 = stream.Seek(100, SeekOrigin.Begin);
            Assert.Equal(100, pos1);
            Assert.Equal(100, stream.Position);

            var pos2 = stream.Seek(50, SeekOrigin.Current);
            Assert.Equal(150, pos2);
            Assert.Equal(150, stream.Position);

            var pos3 = stream.Seek(-100, SeekOrigin.End);
            Assert.Equal(900, pos3);
            Assert.Equal(900, stream.Position);

            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task CancellationToken_ShouldBeRespected()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "cancellation_test.txt");

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

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.ReadAsync(new byte[10], 0, 10, cts.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.WriteAsync(new byte[10], 0, 10, cts.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.FlushAsync(cts.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.ReadAsync(new Memory<byte>(new byte[10]), cts.Token).AsTask());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.WriteAsync(new ReadOnlyMemory<byte>(new byte[10]), cts.Token).AsTask());
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void LargeDataWrite_ShouldHandleChunking()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "large_data_test.txt");

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

            var largeData = new byte[16384];
            for (var i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 256);

            stream.Write(largeData, 0, largeData.Length);
            Assert.Equal(largeData.Length, stream.Position);
            Assert.Equal(largeData.Length, stream.Length);

            stream.Position = 0;
            var readBuffer = new byte[largeData.Length];
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

            Assert.Equal(largeData.Length, bytesRead);
            Assert.Equal(largeData, readBuffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task LargeDataWriteAsync_ShouldHandleChunking()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "large_data_async_test.txt");

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
            var largeData = new byte[20480];
            for (var i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 256);

            await stream.WriteAsync(largeData.AsMemory());
            Assert.Equal(largeData.Length, stream.Position);
            Assert.Equal(largeData.Length, stream.Length);

            stream.Position = 0;
            var readBuffer = new byte[largeData.Length];
            var bytesRead = await stream.ReadAsync(readBuffer.AsMemory());

            Assert.Equal(largeData.Length, bytesRead);
            Assert.Equal(largeData, readBuffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void PartialSectorWrite_ShouldHandleAlignment()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "partial_sector_test.txt");

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

            var testData = new byte[1500];
            for (var i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            stream.Write(testData, 0, testData.Length);
            Assert.Equal(testData.Length, stream.Position);

            stream.Position = 0;
            var readBuffer = new byte[testData.Length];
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

            Assert.Equal(testData.Length, bytesRead);
            Assert.Equal(testData, readBuffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public void StreamProperties_ShouldRemainConsistent()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "properties_consistency_test.txt");

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

            Assert.True(stream.CanRead);
            Assert.True(stream.CanWrite);
            Assert.True(stream.CanSeek);
            Assert.Equal(0, stream.Length);
            Assert.Equal(0, stream.Position);

            var testData = new byte[100];

            stream.Write(testData, 0, testData.Length);
            Assert.Equal(100, stream.Length);
            Assert.Equal(100, stream.Position);

            stream.Position = 50;
            Assert.Equal(50, stream.Position);
            Assert.Equal(100, stream.Length);

            stream.SetLength(150);
            Assert.Equal(150, stream.Length);
            Assert.Equal(50, stream.Position);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task AsyncWrite256Bytes_ShouldWork()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "async_256_test.txt");

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

            var testData = new byte[256];
            for (var i = 0; i < testData.Length; i++)
                testData[i] = (byte)((i + 100) % 256);

            await stream.WriteAsync(testData.AsMemory());
            await stream.FlushAsync();

            stream.Position = 0;

            var buffer = new byte[256];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory());

            Assert.Equal(256, bytesRead);
            Assert.Equal(testData, buffer);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}