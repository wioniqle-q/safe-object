using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SafeObject.Core.Services.Base;
using static SafeObject.Core.Platform.Windows.WindowsKernel;

namespace SafeObject.Core.Services.Platform.Windows;

public sealed class WindowsDirectStream(
    string path,
    FileMode mode,
    FileAccess access,
    FileShare share,
    int bufferSize,
    FileOptions options,
    ILogger? logger)
    : DirectStreamBase(path, mode, access, share, bufferSize, options, logger)
{
    protected override void ConfigurePlatformPropertiesCore()
    {
    }

    protected override Task ExecutePlatformSpecificFlushAsync(CancellationToken cancellationToken)
    {
        if (FlushBuffers(SafeFileHandle) is not true)
            throw new IOException($"FlushFileBuffers failed with error: {Marshal.GetLastWin32Error()}");

        return Task.CompletedTask;
    }
}