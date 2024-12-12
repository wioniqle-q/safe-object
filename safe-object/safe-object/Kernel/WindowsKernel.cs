using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace safe_object.Kernel;

public static partial class WindowsKernel
{
    private const string Kernel32LibraryName = "kernel32.dll";

    [LibraryImport(Kernel32LibraryName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushFileBuffers(SafeFileHandle handle);
    
    [LibraryImport(Kernel32LibraryName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr hObject);
}