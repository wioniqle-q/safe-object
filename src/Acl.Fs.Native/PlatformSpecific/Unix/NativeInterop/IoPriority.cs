using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Acl.Fs.Native.PlatformSpecific.Unix.NativeInterop;

internal static partial class IoPriority
{
    internal static readonly Func<IntPtr> ErrnoLocation;
    internal static readonly Func<long, int, int, int, int> IoPrioSet;

    static IoPriority()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ErrnoLocation = GetErrnoLocation;
            IoPrioSet = SetIoPriority;
        }
        else
        {
            ErrnoLocation = () => IntPtr.Zero;
            IoPrioSet = (_, _, _, _) => 0;
        }
    }

    [SupportedOSPlatform("linux")]
    [LibraryImport(UnixConstants.Libraries.LibcLibraryName, EntryPoint = "__errno_location", SetLastError = true)]
    private static partial IntPtr GetErrnoLocation();

    [SupportedOSPlatform("linux")]
    [LibraryImport(UnixConstants.Libraries.LibcLibraryName, EntryPoint = "syscall", SetLastError = true)]
    private static partial int SetIoPriority(long syscallNumber, int which, int who, int ioprio);
}