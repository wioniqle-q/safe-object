using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Platform.Unix.NativeInterop;

internal static partial class FileOps
{
    [LibraryImport(Constants.Unix.LibcLibraryName, EntryPoint = "posix_fadvise", SetLastError = true)]
    internal static partial int posix_fadvise(SafeFileHandle fd, long offset, long len, int advice);

    [LibraryImport(Constants.Unix.LibcLibraryName, EntryPoint = "fsync", SetLastError = true)]
    internal static partial int fsync(SafeFileHandle fd);
}