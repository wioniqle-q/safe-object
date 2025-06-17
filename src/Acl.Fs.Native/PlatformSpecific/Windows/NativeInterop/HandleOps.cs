using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.Windows.NativeInterop;

internal static partial class HandleOps
{
    [LibraryImport(WindowsConstants.Libraries.Kernel32LibraryName, EntryPoint = "GetHandleInformation",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetHandleInformation(SafeFileHandle hObject, out int lpdwFlags);
}