using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Platform.Unix;
using Xunit;

namespace SafeObject.Core.Tests.Kernel;

public sealed class UnixKernelTests
{
    [Fact]
    public void PosixFadvise_WithValidParameters()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        using var file = File.Open("test.txt", FileMode.Create);
        using var handle = file.SafeFileHandle;

        var result = UnixKernel.PosixFadvise(handle, 0, 1024, 0);

        Assert.Equal(0, result);
    }

    [Fact]
    public void PosixFadvise_WithNullHandle()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        var result = UnixKernel.PosixFadvise(null!, 0, 1024, 0);

        Assert.Equal(-9, result);
    }

    [Fact]
    public void PosixFadvise_WithInvalidHandle()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        using var handle = new SafeFileHandle(new IntPtr(-1), true);

        var result = UnixKernel.PosixFadvise(handle, 0, 1024, 0);

        Assert.Equal(-9, result);
    }

    [Fact]
    public void PosixFadvise_WithNegativeOffset()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        using var file = File.Open("test.txt", FileMode.Create);
        using var handle = file.SafeFileHandle;

        var result = UnixKernel.PosixFadvise(handle, -1, 1024, 0);

        Assert.Equal(-22, result);
    }

    [Fact]
    public void PosixFadvise_WithNegativeAdvice()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        using var file = File.Open("test.txt", FileMode.Create);
        using var handle = file.SafeFileHandle;

        var result = UnixKernel.PosixFadvise(handle, 0, 1024, -1);

        Assert.Equal(-22, result);
    }

    [Fact]
    public void Fsync_WithValidHandle()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        using var file = File.Open("test.txt", FileMode.Create);
        using var handle = file.SafeFileHandle;

        var result = UnixKernel.Fsync(handle);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Fsync_WithNullHandle()
    {
        if (OperatingSystem.IsLinux() is not true)
        {
            Assert.True(true, "Test skipped on non-Unix platform.");
            return;
        }

        var result = UnixKernel.Fsync(null!);

        Assert.Equal(-9, result);
    }
}