namespace Acl.Fs.Stream.Resources;

internal static class LogMessages
{
    internal const string UnixConfiguration = "Configuring Unix-specific stream properties";
    internal const string MacOsConfiguration = "Configuring macOS-specific stream properties";
    internal const string WindowsConfiguration = "Configuring Windows-specific stream properties";
    internal const string IoPriorityFailed = "Failed to set I/O priority. Errno: {0}";
    internal const string PosixFadviseFailed = "posix_fadvise({0}) failed: {1}, Length: {2}";
}