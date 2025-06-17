using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.MacOs.NativeInterop;

internal static partial class FileOps
{
    internal static readonly Func<SafeFileHandle, int, int, int> Fcntl;

    static FileOps()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Fcntl = FileControl;
        else
            Fcntl = (_, _, _) => 0;
    }

    [SupportedOSPlatform("osx")]
    [LibraryImport(MacOsConstants.Libraries.LibcLibraryName, EntryPoint = "fcntl", SetLastError = true)]
    private static partial int FileControl(SafeFileHandle fd, int cmd, int arg);
}