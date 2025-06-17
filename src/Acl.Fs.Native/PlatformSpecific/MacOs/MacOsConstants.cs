namespace Acl.Fs.Native.PlatformSpecific.MacOs;

internal static class MacOsConstants
{
    internal static class Errors
    {
        internal const int EBadF = 9;
    }

    internal static class FileOperations
    {
        internal const int FFullfsync = 51;
    }

    internal static class Libraries
    {
        internal const string LibcLibraryName = "libc";
    }
}