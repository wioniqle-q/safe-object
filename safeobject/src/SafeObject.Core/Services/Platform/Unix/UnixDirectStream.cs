using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SafeObject.Core.Helpers;
using SafeObject.Core.Services.Base;
using static SafeObject.Core.Platform.Unix.UnixKernel;

namespace SafeObject.Core.Services.Platform.Unix;

public sealed class UnixDirectStream(
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
        if (TrySetIoPriority(Constants.Unix.IoPriority.ClassRealTime) is not true &&
            TrySetIoPriority(Constants.Unix.IoPriority.ClassBestEffort) is not true)
            Logger?.LogWarning("Failed to set I/O priority. Errno: {errno}", Marshal.GetLastWin32Error());

        var adv = PosixFadvise(SafeFileHandle, 0, Length, Constants.Unix.FileAdvice.Sequential);
        if (adv is not 0)
            Logger?.LogWarning(
                "posix_fadvise(Sequential) failed: {Result}, Length: {Length}", adv, Length);
    }

    protected override Task ExecutePlatformSpecificFlushAsync(CancellationToken cancellationToken)
    {
        if (Fsync(SafeFileHandle) is not 0)
            throw new IOException($"fsync failed with error: {Marshal.GetLastWin32Error()}");

        var adv = PosixFadvise(SafeFileHandle, 0, Length, Constants.Unix.FileAdvice.DontNeed);
        if (adv is not 0)
            Logger?.LogWarning(
                "posix_fadvise(DontNeed) failed: {Result}, Length: {Length}", adv, Length);

        return Task.CompletedTask;
    }

    private static bool TrySetIoPriority(int prio)
    {
        return SetIoPriority(Constants.Unix.IoPriority.WhoProcess, 0, prio, 0) == 0;
    }
}