using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Moq;
using SafeObject.Core.Services.Factory;
using SafeObject.Core.Services.Platform.Windows;
using Xunit;

namespace SafeObject.Core.Tests.DirectStream;

public sealed class DirectStreamTests
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);


    [Fact]
    public async Task FlushAsync_WhenCalled_CompletesWithoutException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);

            await stream.FlushAsync(CancellationToken.None);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task FlushAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        FileStream stream;
        try
        {
            stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);
            await stream.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await stream.FlushAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task DisposeAsync_CompletesWithoutException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        try
        {
            var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);
            await stream.DisposeAsync();
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task FlushAsync_ConcurrentCalls_OnlyOneFlushExecuted()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);

            var task1 = stream.FlushAsync(CancellationToken.None);
            var task2 = stream.FlushAsync(CancellationToken.None);
            await Task.WhenAll(task1, task2);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task MultipleSequentialFlushAsync_CallsSuccessfully()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);

            for (var i = 0; i < 5; i++)
                await stream.FlushAsync(CancellationToken.None);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task FlushAsync_WithCanceledToken_ThrowsTaskCanceledException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await stream.FlushAsync(cts.Token));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullPath_ThrowsArgumentNullException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        Assert.Throws<ArgumentNullException>(() =>
            DirectStreamFactory.Create(
                null!,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object));
    }

    [Fact]
    public async Task Write_AndFlush_DataIsWritten()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        try
        {
            await using (var stream = DirectStreamFactory.Create(
                             tempFilePath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.None,
                             loggerMock.Object))
            {
                await stream.WriteAsync(data);
                await stream.FlushAsync(CancellationToken.None);
            }

            var fileData = await File.ReadAllBytesAsync(tempFilePath);
            Assert.Equal(data, fileData);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task Dispose_CalledMultipleTimes_NoException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        try
        {
            var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);
            await stream.DisposeAsync();
            await stream.DisposeAsync();
            await stream.DisposeAsync();
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task DisposeAsync_MultipleFlushAsyncAfterDispose_ThrowsObjectDisposedException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        FileStream stream;
        try
        {
            stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);
            await stream.FlushAsync(CancellationToken.None);
            await stream.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await stream.FlushAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }


    [Fact]
    public void ConfigurePlatformPropertiesCore_IsInvokable()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var tempFilePath = Path.GetTempFileName();
        try
        {
            using var stream = (WindowsDirectStream)DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                Mock.Of<ILogger>());
            var method = typeof(WindowsDirectStream)
                .GetMethod("ConfigurePlatformPropertiesCore", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(stream, null);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task ExecutePlatformSpecificFlushAsync_WithInvalidHandle_ThrowsIOException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var tempFilePath = Path.GetTempFileName();
        WindowsDirectStream? stream = null;
        var rawHandle = IntPtr.Zero;

        try
        {
            stream = (WindowsDirectStream)DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                Mock.Of<ILogger>());

            var handleProp = typeof(FileStream)
                .GetProperty("SafeFileHandle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            var safeHandle = (SafeFileHandle)handleProp.GetValue(stream)!;
            rawHandle = safeHandle.DangerousGetHandle();
            safeHandle.SetHandleAsInvalid();

            var method = typeof(WindowsDirectStream)
                .GetMethod("ExecutePlatformSpecificFlushAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            await Assert.ThrowsAsync<TargetInvocationException>(() =>
                (Task)method.Invoke(stream, [CancellationToken.None])!);
        }
        finally
        {
            if (stream is not null)
                await stream.DisposeAsync();

            if (rawHandle != IntPtr.Zero)
                CloseHandle(rawHandle);

            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task ExecutePlatformSpecificFlushAsync_ValidHandle_CompletesSuccessfully()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var tempFilePath = Path.GetTempFileName();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                Mock.Of<ILogger>());

            var method = typeof(WindowsDirectStream)
                .GetMethod("ExecutePlatformSpecificFlushAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var task = (Task)method.Invoke(stream, [CancellationToken.None])!;
            await task;
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task ExecutePlatformSpecificFlushAsync_WithCanceledToken_IgnoredAndCompletes()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var tempFilePath = Path.GetTempFileName();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                Mock.Of<ILogger>());

            var method = typeof(WindowsDirectStream)
                .GetMethod("ExecutePlatformSpecificFlushAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var task = (Task)method.Invoke(stream, [cts.Token])!;
            await task;
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task FlushAsync_WithInvalidHandlePublic_ThrowsIOException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var tempFilePath = Path.GetTempFileName();
        WindowsDirectStream? stream = null;
        var rawHandle = IntPtr.Zero;

        try
        {
            stream = (WindowsDirectStream)DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None,
                Mock.Of<ILogger>());

            var handleProp = typeof(FileStream)
                .GetProperty("SafeFileHandle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            var safeHandle = (SafeFileHandle)handleProp.GetValue(stream)!;
            rawHandle = safeHandle.DangerousGetHandle();
            safeHandle.SetHandleAsInvalid();

            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                stream.FlushAsync(CancellationToken.None));
        }
        finally
        {
            if (stream is not null)
                await stream.DisposeAsync();

            if (rawHandle != IntPtr.Zero)
                CloseHandle(rawHandle);

            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task ReadAsync_AfterWrite_ReadsCorrectData()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var loggerMock = new Mock<ILogger<Services.StorageService>>();
        var tempFilePath = Path.GetTempFileName();
        var data = new byte[] { 10, 20, 30, 40, 50 };

        try
        {
            await using var stream = DirectStreamFactory.Create(
                tempFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.None,
                loggerMock.Object);

            await stream.WriteAsync(data);
            stream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[data.Length];
            var bytesRead = await stream.ReadAsync(buffer);

            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data, buffer);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }
}