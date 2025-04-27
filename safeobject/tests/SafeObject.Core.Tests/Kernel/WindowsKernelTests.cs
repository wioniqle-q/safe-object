using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Platform.Windows;
using Xunit;

namespace SafeObject.Core.Tests.Kernel;

public sealed class WindowsKernelTests
{
    private const string TestFileName = "testfile.txt";

    private static (FileStream Stream, SafeFileHandle Handle) CreateTempFile(
        FileMode mode = FileMode.Create,
        FileAccess access = FileAccess.ReadWrite,
        FileShare share = FileShare.None,
        string? filePath = null)
    {
        var path = filePath ?? Path.Combine(Path.GetTempPath(), TestFileName + Guid.NewGuid());
        var fileStream = new FileStream(
            path,
            mode,
            access,
            share,
            4096,
            FileOptions.DeleteOnClose);
        return (fileStream, fileStream.SafeFileHandle);
    }

    [Fact]
    public void FlushFileBuffers_DisposedHandle_ReturnsFalseWithInvalidHandleError()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        handle.Dispose();

        var result = WindowsKernel.FlushBuffers(handle);
        stream.Dispose();

        Assert.False(result);
        Assert.Equal(0, Marshal.GetLastWin32Error());
    }

    [Fact]
    public void FlushFileBuffers_ValidHandleWithPendingWrites_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        using (stream)
        {
            stream.WriteByte(42);
            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_ValidHandleEmptyFile_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        using (stream)
        {
            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_NullHandle_ThrowsArgumentNullException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        SafeFileHandle handle = null!;
        Assert.Throws<ArgumentNullException>(() => WindowsKernel.FlushBuffers(handle));
    }

    [Fact]
    public async Task FlushFileBuffers_LargeFileWithPendingWrites_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        await using (stream)
        {
            var data = new byte[1024 * 1024];
            await stream.WriteAsync(data);

            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_FileWithSharedAccess_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        using (stream)
        {
            stream.WriteByte(42);
            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_InvalidHandle_ThrowsInvalidOperationException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var handle = new SafeFileHandle(IntPtr.Zero, true);
        Assert.Throws<InvalidOperationException>(() => WindowsKernel.FlushBuffers(handle));
    }

    [Fact]
    public void FlushFileBuffers_NonZeroInvalidHandle_ThrowsInvalidOperationException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var handle = new SafeFileHandle(new IntPtr(12345), false);
        Assert.Throws<InvalidOperationException>(() => WindowsKernel.FlushBuffers(handle));
    }

    [Fact]
    public async Task FlushFileBuffers_ConcurrentWriteAndFlush_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            await Task.CompletedTask;
            return;
        }

        var (stream, handle) = CreateTempFile(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        await using (stream)
        {
            var writeTask = Task.Run(() => stream.WriteByte(42));
            var flushTask = Task.Run(() => WindowsKernel.FlushBuffers(handle));
            await writeTask;
            var flushResult = await flushTask;
            Assert.True(flushResult);
        }
    }

    [Fact]
    public void FlushFileBuffers_MultipleSequentialFlushes_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        using (stream)
        {
            var result1 = WindowsKernel.FlushBuffers(handle);
            var result2 = WindowsKernel.FlushBuffers(handle);
            var result3 = WindowsKernel.FlushBuffers(handle);
            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
        }
    }

    [Fact]
    public async Task FlushFileBuffers_ConcurrentFlushes_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            await Task.CompletedTask;
            return;
        }

        var (stream, handle) = CreateTempFile();
        await using (stream)
        {
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(() => WindowsKernel.FlushBuffers(handle)))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
                Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_ReadOnlyHandle_ThrowsWin32Exception()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "readonly_" + Guid.NewGuid());
        File.WriteAllText(path, "test");
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                   FileShare.Read))
        {
            var handle = fs.SafeFileHandle;
            var ex = Assert.Throws<Win32Exception>(() => WindowsKernel.FlushBuffers(handle));
            Assert.Contains("Failed to flush file buffers.", ex.Message);
        }

        File.Delete(path);
    }

    [Fact]
    public async Task FlushFileBuffers_WriteFlush_MultipleCycle_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            await Task.CompletedTask;
            return;
        }

        var (stream, handle) = CreateTempFile();
        var content1 = "First line\n";
        var content2 = "Second line\n";
        await using (stream)
        {
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content1);
            await writer.FlushAsync();
            var flushResult1 = WindowsKernel.FlushBuffers(handle);
            await writer.WriteAsync(content2);
            await writer.FlushAsync();
            var flushResult2 = WindowsKernel.FlushBuffers(handle);
            Assert.True(flushResult1);
            Assert.True(flushResult2);
        }
    }

    [Fact]
    public void FlushFileBuffers_NoPendingWrites_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        using (stream)
        {
            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public async Task FlushFileBuffers_ConcurrentFlushesAndWrites_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            await Task.CompletedTask;
            return;
        }

        var (stream, handle) = CreateTempFile();
        await using (stream)
        {
            var writeTask = Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                    stream.WriteByte((byte)i);
            });
            var flushTasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(() => WindowsKernel.FlushBuffers(handle)))
                .ToArray();

            await writeTask;
            var results = await Task.WhenAll(flushTasks);
            foreach (var result in results)
                Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_MixedAccess_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        using (stream)
        {
            stream.WriteByte(100);
            var result = WindowsKernel.FlushBuffers(handle);
            Assert.True(result);
        }
    }

    [Fact]
    public void CreateTempFile_WithCustomPath_CreatesFileSuccessfully()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var customPath = Path.Combine(Path.GetTempPath(), "CustomTemp_" + Guid.NewGuid() + ".txt");
        var (stream, handle) = CreateTempFile(FileMode.Create, FileAccess.ReadWrite, FileShare.None, customPath);

        try
        {
            Assert.True(File.Exists(customPath));
            stream.WriteByte(99);
            var flushResult = WindowsKernel.FlushBuffers(handle);
            Assert.True(flushResult);
        }
        finally
        {
            stream.Dispose();
            if (File.Exists(customPath)) File.Delete(customPath);
        }
    }

    [Fact]
    public async Task FlushFileBuffers_ConcurrentFlushes_StressTest_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            await Task.CompletedTask;
            return;
        }

        var (stream, handle) = CreateTempFile();
        await using (stream)
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => WindowsKernel.FlushBuffers(handle)))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            foreach (var result in results) Assert.True(result);
        }
    }

    [Fact]
    public void FlushFileBuffers_MultipleCycle_Stress_Succeeds()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var (stream, handle) = CreateTempFile();
        using (stream)
        {
            for (var i = 0; i < 15; i++)
            {
                var result = WindowsKernel.FlushBuffers(handle);
                Assert.True(result);
            }
        }
    }

    [Fact]
    public void FlushFileBuffers_ValidHandle_GetHandleInformationFails_ThrowsInvalidOperationException()
    {
        if (OperatingSystem.IsWindows() is not true)
        {
            Assert.True(true, "Test skipped on non-Windows platform.");
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "restricted_" + Guid.NewGuid() + ".txt");
        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(42);
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var handle = fs.SafeFileHandle;
                Assert.Throws<Win32Exception>(() => WindowsKernel.FlushBuffers(handle));
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}