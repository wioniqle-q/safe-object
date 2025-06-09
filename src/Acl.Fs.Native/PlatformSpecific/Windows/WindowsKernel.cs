using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Acl.Fs.Native.PlatformSpecific.Windows.NativeInterop;
using Acl.Fs.Native.Resources;
using Microsoft.Win32.SafeHandles;

namespace Acl.Fs.Native.PlatformSpecific.Windows;

internal static class WindowsKernel
{
    private static readonly Func<SafeFileHandle, bool> FlushFileBuffers;

    static WindowsKernel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            FlushFileBuffers = FlushFileBuffersInternal;
        else
            FlushFileBuffers = _ => false;
    }

    internal static bool FlushBuffers(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle, NativeErrorMessages.FileHandleCannotBeNull);

        if (handle.IsClosed)
            return false;

        if (handle.IsInvalid)
            throw new InvalidOperationException(NativeErrorMessages.FileHandleInvalid);

        if (HandleOps.GetHandleInformation(handle, out _) is not true)
            throw new InvalidOperationException(NativeErrorMessages.HandleStaleOrInvalid);

        if (FlushFileBuffers(handle))
            return true;

        throw new Win32Exception(Marshal.GetLastWin32Error(), NativeErrorMessages.FailedToFlushBuffers);
    }

    [SupportedOSPlatform("windows")]
    private static bool FlushFileBuffersInternal(SafeFileHandle handle)
    {
        return FileOps.FlushFileBuffers(handle);
    }
}