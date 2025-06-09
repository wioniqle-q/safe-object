using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.MacOs;
using Acl.Fs.Stream.Abstractions;
using Acl.Fs.Stream.Resources;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Stream.Implementations;

internal sealed class MacOsDirectStream(
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
        Logger?.LogDebug(LogMessages.MacOsConfiguration);
    }

    protected override void ExecutePlatformSpecificFlush(CancellationToken cancellationToken)
    {
        if (MacOsKernel.FullFsync(InnerStream.SafeFileHandle) is not 0)
            throw new IOException(string.Format(ErrorMessages.MacOsFullFsyncFailed, Marshal.GetLastWin32Error()));
    }
}