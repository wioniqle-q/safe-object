using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Platform.Windows.NativeInterop;

internal static partial class HandleOps
{
    [LibraryImport(Constants.Windows.Kernel32LibraryName, EntryPoint = "GetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetHandleInformation(SafeFileHandle hObject, out int lpdwFlags);
}