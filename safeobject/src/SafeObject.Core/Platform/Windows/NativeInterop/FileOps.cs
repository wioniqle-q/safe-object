using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Platform.Windows.NativeInterop;

internal static partial class FileOps
{
    [LibraryImport(Constants.Windows.Kernel32LibraryName, EntryPoint = "FlushFileBuffers", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FlushFileBuffers(SafeFileHandle handle);
}