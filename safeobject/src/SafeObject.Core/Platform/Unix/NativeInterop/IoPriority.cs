using System.Runtime.InteropServices;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Platform.Unix.NativeInterop;

internal static partial class IoPriority
{
    [LibraryImport(Constants.Unix.LibcLibraryName, EntryPoint = "__errno_location", SetLastError = true)]
    internal static partial IntPtr __errno_location();

    [LibraryImport(Constants.Unix.LibcLibraryName, EntryPoint = "syscall", SetLastError = true)]
    internal static partial int ioprio_set(long syscallNumber, int which, int who, int ioprio);
}
