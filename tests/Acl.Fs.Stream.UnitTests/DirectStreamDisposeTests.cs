using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acl.Fs.Stream.UnitTests;

public sealed class DirectStreamDisposeTests
{
    private readonly ILogger _logger;
    private readonly string _testDirectoryPath;

    public DirectStreamDisposeTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), "DirectStreamDisposeTests");
        Directory.CreateDirectory(_testDirectoryPath);
        _logger = NullLogger.Instance;
    }

    [Fact]
    public void Dispose_ShouldPreventFurtherOperations()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "dispose_test.txt");

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

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public async Task DisposeAsync_ShouldPreventFurtherOperations()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "dispose_async_test.txt");
        var stream = DirectStreamFactory.Create(
            testFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.None,
            _logger);

        await stream.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[10], 0, 10));
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[10], 0, 10));
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => stream.SetLength(100));

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public void MultipleDispose_ShouldBeIdempotent()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "multiple_dispose_test.txt");
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

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public async Task DisposedStream_AsyncOperations_ShouldThrowObjectDisposedException()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "disposed_async_test.txt");
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
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(new Memory<byte>(new byte[10])).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.WriteAsync(new ReadOnlyMemory<byte>(new byte[10])).AsTask());

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public async Task MultipleDisposeAsync_ShouldBeIdempotent()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "multiple_dispose_async_test.txt");
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

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }

    [Fact]
    public async Task DisposeAsync_WithNonAsyncDisposableStream_ShouldUseRegularDisposeAsync()
    {
        var testFilePath = Path.Combine(_testDirectoryPath, "dispose_async_regular_test.txt");

        var stream = DirectStreamFactory.Create(
            testFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.None,
            _logger);

        await stream.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[10], 0, 10));

        if (File.Exists(testFilePath))
            File.Delete(testFilePath);
    }
}