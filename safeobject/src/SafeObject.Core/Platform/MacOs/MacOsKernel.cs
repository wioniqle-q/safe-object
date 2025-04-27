using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;
using SafeObject.Core.Platform.MacOS.NativeInterop;

namespace SafeObject.Core.Platform.MacOS;

public static class MacOsKernel
{
    public static int FullFsync(SafeFileHandle? fd)
    {
        if (OperatingSystem.IsMacOS() is not true)
            throw new PlatformNotSupportedException("This operation is only supported on macOS.");

        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -Constants.Unix.Errors.EBadF;

        var result = FileOps.fcntl(fd, Constants.MacOs.FFullfsync, 0);
        return result is -1 ? -Marshal.GetLastWin32Error() : result;
    }
}