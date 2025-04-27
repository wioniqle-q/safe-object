using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SafeObject.Core.Platform.Windows.NativeInterop;

namespace SafeObject.Core.Platform.Windows;

public static class WindowsKernel
{
    public static bool FlushBuffers(SafeFileHandle handle)
    {
        if (handle is null)
            throw new ArgumentNullException(nameof(handle), "File handle cannot be null.");

        if (handle.IsClosed)
            return false;

        if (handle.IsInvalid)
            throw new InvalidOperationException("The file handle is invalid or has been marked as invalid.");

        if (HandleOps.GetHandleInformation(handle, out _) is not true)
            throw new InvalidOperationException("The handle is stale or invalid.");

        if (FileOps.FlushFileBuffers(handle))
            return true;

        var errorCode = Marshal.GetLastWin32Error();
        throw new Win32Exception(errorCode, "Failed to flush file buffers.");
    }
}