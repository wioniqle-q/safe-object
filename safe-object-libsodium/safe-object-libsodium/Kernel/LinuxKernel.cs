using System.Runtime.InteropServices;

namespace safe_object.Kernel;

public static partial class LinuxKernel
{
    private const string LibcLibraryName = "libc";

    [LibraryImport(LibcLibraryName, SetLastError = true)]
    public static partial int posix_fadvise(
        int fd,
        long offset,
        long len,
        int advice);

    [LibraryImport(LibcLibraryName, SetLastError = true)]
    public static partial int fsync(int fd);

    [LibraryImport(LibcLibraryName, SetLastError = true)]
    public static partial int close(int fd);

    [DllImport(LibcLibraryName, SetLastError = true, EntryPoint = "syscall")]
    private static extern int ioprio_set(long syscallNumber, int which, int who, int ioprio);

    public static int SetIoPriority(int which, int who, int ioClass, int priority)
    {
        return ioprio_set(Constants.Linux.IoPriority.SysSet, which, who,
            (ioClass << Constants.Linux.IoPriority.ClassShift) | priority);
    }
}