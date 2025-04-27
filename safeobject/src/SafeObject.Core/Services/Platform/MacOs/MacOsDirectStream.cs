using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SafeObject.Core.Services.Base;
using static SafeObject.Core.Platform.MacOS.MacOsKernel;

namespace SafeObject.Core.Services.Platform.MacOS;

public sealed class MacOsDirectStream(
    string path,
    FileMode mode,
    FileAccess access,
    FileShare share,
    int bufferSize,
    FileOptions options,
    ILogger? logger
) : DirectStreamBase(path, mode, access, share, bufferSize, options, logger)
{
    protected override void ConfigurePlatformPropertiesCore()
    {
    }

    protected override Task ExecutePlatformSpecificFlushAsync(CancellationToken _)
    {
        if (FullFsync(SafeFileHandle) is not 0)
            throw new IOException($"Full fsync failed with error: {Marshal.GetLastWin32Error()}");
        return Task.CompletedTask;
    }
}