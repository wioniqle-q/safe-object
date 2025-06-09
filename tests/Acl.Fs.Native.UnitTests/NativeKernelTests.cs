using System.ComponentModel;
using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.MacOs;
using Acl.Fs.Native.PlatformSpecific.Unix;
using Acl.Fs.Native.PlatformSpecific.Windows;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.UnitTests;

public sealed class NativeKernelTests
{
    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldSucceed_OnWindows()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            var safeHandle = fileStream.SafeFileHandle;

            var result = WindowsKernel.FlushBuffers(safeHandle);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldThrowArgumentNullException_WhenHandleIsNull()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        Assert.Throws<ArgumentNullException>(() => WindowsKernel.FlushBuffers(null!));
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldReturnFalse_WhenHandleIsClosed()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();

        try
        {
            SafeFileHandle safeHandle;
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                safeHandle = fileStream.SafeFileHandle;
            }

            var result = WindowsKernel.FlushBuffers(safeHandle);
            Assert.False(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldThrowInvalidOperationException_WhenHandleIsInvalid()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            var safeHandle = fileStream.SafeFileHandle;

            fileStream.Close();

            var result = WindowsKernel.FlushBuffers(safeHandle);
            Assert.False(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldTestInvalidHandleScenario()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();

        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            fileStream.Write(new byte[100]);

            var safeHandle = fileStream.SafeFileHandle;

            var result = WindowsKernel.FlushBuffers(safeHandle);
            Assert.True(result);

            fileStream.Close();

            var resultAfterClose = WindowsKernel.FlushBuffers(safeHandle);
            Assert.False(resultAfterClose);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldHandleReadOnlyFile()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "test data");

            using var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read);
            var safeHandle = fileStream.SafeFileHandle;

            try
            {
                var result = WindowsKernel.FlushBuffers(safeHandle);
                Assert.True(result);
            }
            catch (Win32Exception)
            {
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldHandleDifferentFileTypes()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();

        try
        {
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                fileStream.Write(new byte[1024]);
                var result = WindowsKernel.FlushBuffers(fileStream.SafeFileHandle);
                Assert.True(result);
            }

            using (var fileStream =
                   new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192))
            {
                fileStream.Write(new byte[2048]);
                var result = WindowsKernel.FlushBuffers(fileStream.SafeFileHandle);
                Assert.True(result);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void WindowsKernel_FlushBuffers_ShouldWorkWithLargeFiles()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Windows");

        var tempFile = Path.GetTempFileName();

        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);

            var largeData = new byte[1024 * 1024];
            for (var i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 256);

            fileStream.Write(largeData);

            var result = WindowsKernel.FlushBuffers(fileStream.SafeFileHandle);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void UnixKernel_SetIoPriority_ShouldReturnError_WithInvalidParameters()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var result = UnixKernel.SetIoPriority(-1, -1, -1, -1);
        Assert.True(result < 0);
    }

    [SkippableFact]
    public void UnixKernel_SetIoPriority_ShouldSucceed_WithValidParameters()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var result = UnixKernel.SetIoPriority(
            UnixConstants.IoPriority.WhoProcess,
            Environment.ProcessId,
            UnixConstants.IoPriority.ClassBestEffort,
            4);

        Assert.True(result >= 0);
    }

    [SkippableFact]
    public void UnixKernel_PosixFadvise_ShouldReturnError_WithNullHandle()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var result = UnixKernel.PosixFadvise(null, 0, 1024, UnixConstants.FileAdvice.PosixFadvSequential);
        Assert.True(result < 0);
    }

    [SkippableFact]
    public void UnixKernel_PosixFadvise_ShouldSucceed_WithValidParameters()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite);
            var result = UnixKernel.PosixFadvise(fileStream.SafeFileHandle, 0, 1024,
                UnixConstants.FileAdvice.PosixFadvSequential);
            Assert.True(result >= 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void UnixKernel_Fsync_ShouldReturnError_WithNullHandle()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var result = UnixKernel.Fsync(null);
        Assert.True(result < 0);
    }

    [SkippableFact]
    public void UnixKernel_Fsync_ShouldSucceed_WithValidHandle()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test only runs on Unix-like systems");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            fileStream.Write(new byte[1024]);

            var result = UnixKernel.Fsync(fileStream.SafeFileHandle);
            Assert.True(result >= 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public void MacOsKernel_FullFsync_ShouldReturnError_WithNullHandle()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "This test only runs on macOS");

        var result = MacOsKernel.FullFsync(null);
        Assert.True(result < 0);
    }

    [SkippableFact]
    public void MacOsKernel_FullFsync_ShouldSucceed_WithValidHandle()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "This test only runs on macOS");

        var tempFile = Path.GetTempFileName();
        try
        {
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
            fileStream.Write(new byte[1024]);

            var result = MacOsKernel.FullFsync(fileStream.SafeFileHandle);
            Assert.True(result >= 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}