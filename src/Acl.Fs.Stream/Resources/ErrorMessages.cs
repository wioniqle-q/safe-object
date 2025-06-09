namespace Acl.Fs.Stream.Resources;

internal static class ErrorMessages
{
    internal const string UnsupportedPlatform =
        "The current operating system platform is not supported by the direct stream implementation.";

    internal const string UnixFsyncFailed = "fsync failed with error: {0}";
    internal const string MacOsFullFsyncFailed = "Full fsync failed with error: {0}";
    internal const string WindowsFlushBuffersFailed = "FlushFileBuffers failed with error: {0}";
}