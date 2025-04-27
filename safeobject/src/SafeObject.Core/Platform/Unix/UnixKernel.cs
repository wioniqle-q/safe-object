using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;
using FileOps = SafeObject.Core.Platform.Unix.NativeInterop.FileOps;
using IoPriority = SafeObject.Core.Platform.Unix.NativeInterop.IoPriority;

namespace SafeObject.Core.Platform.Unix;

public static class UnixKernel
{
    private static void EnsureUnix()
    {
        if (OperatingSystem.IsLinux() is not true)
            throw new PlatformNotSupportedException("This operation is only supported on Unix.");
    }

    private static int GetLastError()
    {
        var errnoPtr = IoPriority.__errno_location();
        return errnoPtr != IntPtr.Zero ? Marshal.ReadInt32(errnoPtr) : 0;
    }

    public static int SetIoPriority(int which, int who, int ioClass, int priority)
    {
        EnsureUnix();

        if (which < 0 || who < 0 || ioClass < 0 || priority < 0)
            return -Constants.Unix.Errors.EInval;

        var ioprio = (ioClass << Constants.Unix.IoPriority.ClassShift) | priority;
        var result = IoPriority.ioprio_set(Constants.Unix.IoPriority.SysSet, which, who, ioprio);

        return result is -1 ? -GetLastError() : result;
    }

    public static int PosixFadvise(SafeFileHandle? fd, long offset, long len, int advice)
    {
        EnsureUnix();

        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -Constants.Unix.Errors.EBadF;

        if (offset < 0 || len < 0 || advice < 0)
            return -Constants.Unix.Errors.EInval;

        var result = FileOps.posix_fadvise(fd, offset, len, advice);
        return result is -1 ? -GetLastError() : result;
    }

    public static int Fsync(SafeFileHandle? fd)
    {
        EnsureUnix();

        if (fd is null || fd.IsClosed || fd.IsInvalid)
            return -Constants.Unix.Errors.EBadF;

        var result = FileOps.fsync(fd);
        return result is -1 ? -GetLastError() : result;
    }
}