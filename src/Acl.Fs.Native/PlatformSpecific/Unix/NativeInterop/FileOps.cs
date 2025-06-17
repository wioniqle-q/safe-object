using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.Unix.NativeInterop;

internal static partial class FileOps
{
    internal static readonly Func<SafeFileHandle, long, long, int, int> PosixFAdvise;
    internal static readonly Func<SafeFileHandle, int> FSync;

    static FileOps()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PosixFAdvise = Posix_Fadvise;
            FSync = Fsync;
        }
        else
        {
            PosixFAdvise = (_, _, _, _) => 0;
            FSync = _ => 0;
        }
    }

    [SupportedOSPlatform("linux")]
    [LibraryImport(UnixConstants.Libraries.LibcLibraryName, EntryPoint = "posix_fadvise", SetLastError = true)]
    private static partial int Posix_Fadvise(SafeFileHandle fd, long offset, long len, int advice);

    [SupportedOSPlatform("linux")]
    [LibraryImport(UnixConstants.Libraries.LibcLibraryName, EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(SafeFileHandle fd);
}