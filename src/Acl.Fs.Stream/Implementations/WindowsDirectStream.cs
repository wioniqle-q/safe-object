using System.Runtime.InteropServices;
using Acl.Fs.Native.PlatformSpecific.Windows;
using Acl.Fs.Stream.Abstractions;
using Acl.Fs.Stream.Resources;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Stream.Implementations;

internal sealed class WindowsDirectStream(
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
        Logger?.LogDebug(LogMessages.WindowsConfiguration);
    }

    protected override void ExecutePlatformSpecificFlush(CancellationToken cancellationToken)
    {
        if (WindowsKernel.FlushBuffers(InnerStream.SafeFileHandle) is not true)
            throw new IOException(string.Format(ErrorMessages.WindowsFlushBuffersFailed, Marshal.GetLastWin32Error()));
    }
}