using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.Windows.NativeInterop;

internal static partial class FileOps
{
    [LibraryImport(WindowsConstants.Libraries.Kernel32LibraryName, EntryPoint = "FlushFileBuffers",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FlushFileBuffers(SafeFileHandle handle);
}