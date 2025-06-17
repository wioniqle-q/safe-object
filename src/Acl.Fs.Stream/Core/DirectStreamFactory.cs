using Acl.Fs.Stream.Implementations;
using Acl.Fs.Stream.Resources;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Stream.Core;

internal static class DirectStreamFactory
{
    internal static System.IO.Stream Create(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        ILogger? logger = null)
    {
        if (OperatingSystem.IsLinux())
            return new UnixDirectStream(path, mode, access, share, bufferSize, options, logger);

        if (OperatingSystem.IsMacOS())
            return new MacOsDirectStream(path, mode, access, share, bufferSize, options, logger);

        if (OperatingSystem.IsWindows())
            return new WindowsDirectStream(path, mode, access, share, bufferSize, options, logger);

        throw new PlatformNotSupportedException(ErrorMessages.UnsupportedPlatform);
    }
}