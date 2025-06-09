using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.Unix.NativeInterop;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.Unix;

internal static class UnixKernel
{
    private static int GetLastError()
    {
        var errnoPtr = IoPriority.ErrnoLocation();
        return errnoPtr != IntPtr.Zero ? Marshal.ReadInt32(errnoPtr) : 0;
    }

    internal static int SetIoPriority(int which, int who, int ioClass, int priority)
    {
        if (which < 0 || who < 0 || ioClass < 0 || priority < 0)
            return -UnixConstants.Errors.EInval;

        var ioprio = (ioClass << UnixConstants.IoPriority.ClassShift) | priority;
        var result = IoPriority.IoPrioSet(UnixConstants.IoPriority.SysSet, which, who, ioprio);

        return result is -1 ? -GetLastError() : result;
    }

    internal static int PosixFadvise(SafeFileHandle? fd, long offset, long len, int advice)
    {
        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -UnixConstants.Errors.EBadF;

        if (offset < 0 || len < 0 || advice < 0)
            return -UnixConstants.Errors.EInval;

        var result = FileOps.PosixFAdvise(fd, offset, len, advice);
        return result is -1 ? -GetLastError() : result;
    }

    internal static int Fsync(SafeFileHandle? fd)
    {
        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -UnixConstants.Errors.EBadF;

        var result = FileOps.FSync(fd);
        return result is -1 ? -GetLastError() : result;
    }
}