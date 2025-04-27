using Microsoft.Extensions.Logging;
using SafeObject.Core.Services.Platform.MacOS;
using SafeObject.Core.Services.Platform.Unix;
using SafeObject.Core.Services.Platform.Windows;

namespace SafeObject.Core.Services.Factory;

public static class DirectStreamFactory
{
    public static FileStream Create(
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

        throw new PlatformNotSupportedException("Unsupported OS platform");
    }
}