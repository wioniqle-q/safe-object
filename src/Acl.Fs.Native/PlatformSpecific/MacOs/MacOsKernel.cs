using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.MacOs.NativeInterop;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.MacOs;

internal static class MacOsKernel
{
    internal static int FullFsync(SafeFileHandle? fd)
    {
        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -MacOsConstants.Errors.EBadF;

        var result = FileOps.Fcntl(fd, MacOsConstants.FileOperations.FFullfsync, 0);
        return result is -1 ? -Marshal.GetLastWin32Error() : result;
    }
}