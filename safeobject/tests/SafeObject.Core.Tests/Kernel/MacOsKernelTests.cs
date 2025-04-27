using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Platform.MacOS;
using Xunit;

namespace SafeObject.Core.Tests.Kernel;

public sealed class MacOsKernelTests
{
    [Fact]
    public void FullFsync_WithValidHandle()
    {
        if (OperatingSystem.IsMacOS() is not true)
        {
            Assert.True(true, "Test skipped on non-macOS platform.");
            return;
        }

        using var file = File.Open("test.txt", FileMode.Create);
        using var handle = file.SafeFileHandle;

        var result = MacOsKernel.FullFsync(handle);

        Assert.Equal(0, result);
    }

    [Fact]
    public void FullFsync_WithNullHandle()
    {
        if (OperatingSystem.IsMacOS() is not true)
        {
            Assert.True(true, "Test skipped on non-macOS platform.");
            return;
        }

        var result = MacOsKernel.FullFsync(null!);

        Assert.Equal(-9, result);
    }

    [Fact]
    public void FullFsync_WithInvalidHandle()
    {
        if (OperatingSystem.IsMacOS() is not true)
        {
            Assert.True(true, "Test skipped on non-macOS platform.");
            return;
        }

        using var handle = new SafeFileHandle(new IntPtr(-1), true);

        var result = MacOsKernel.FullFsync(handle);

        Assert.Equal(-9, result);
    }
}