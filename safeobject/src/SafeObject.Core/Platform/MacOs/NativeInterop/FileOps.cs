using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Platform.MacOS.NativeInterop;

internal static partial class FileOps
{
    [LibraryImport(Constants.MacOs.LibcLibraryName, EntryPoint = "fcntl", SetLastError = true)]
    internal static partial int fcntl(SafeFileHandle fd, int cmd, int arg);
}