using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.Unix;
using Acl.Fs.Stream.Abstractions;
using Acl.Fs.Stream.Resources;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Stream.Implementations;

internal sealed class UnixDirectStream(
    string path,
    FileMode mode,
    FileAccess access,
    FileShare share,
    int bufferSize,
    FileOptions options,
    ILogger? logger = null)
    : DirectStreamBase<FileStream>(
        new FileStream(path ?? throw new ArgumentNullException(nameof(path)), mode, access, share, bufferSize, options),
        logger)
{
    protected override void ConfigurePlatformPropertiesCore()
    {
        if (TrySetIoPriority(UnixConstants.IoPriority.ClassRealTime) is not true &&
            TrySetIoPriority(UnixConstants.IoPriority.ClassBestEffort) is not true)
            Logger?.LogWarning(LogMessages.IoPriorityFailed, Marshal.GetLastWin32Error());

        var seqResult = UnixKernel.PosixFadvise(InnerStream.SafeFileHandle, 0, InnerStream.Length,
            UnixConstants.FileAdvice.PosixFadvSequential);
        if (seqResult is not 0)
            Logger?.LogWarning(
                LogMessages.PosixFadviseFailed, "Sequential", seqResult, InnerStream.Length);

        var dontNeedResult = UnixKernel.PosixFadvise(InnerStream.SafeFileHandle, 0, InnerStream.Length,
            UnixConstants.FileAdvice.PosixFadvDontNeed);
        if (dontNeedResult is not 0)
            Logger?.LogWarning(
                LogMessages.PosixFadviseFailed, "DontNeed", dontNeedResult, InnerStream.Length);

        Logger?.LogDebug(LogMessages.UnixConfiguration);
    }

    protected override void ExecutePlatformSpecificFlush(CancellationToken cancellationToken)
    {
        if (UnixKernel.Fsync(InnerStream.SafeFileHandle) is not 0)
            throw new IOException(string.Format(ErrorMessages.UnixFsyncFailed, Marshal.GetLastWin32Error()));
    }

    private static bool TrySetIoPriority(int prio)
    {
        return UnixKernel.SetIoPriority(UnixConstants.IoPriority.WhoProcess, 0, prio, 0) is 0;
    }
}