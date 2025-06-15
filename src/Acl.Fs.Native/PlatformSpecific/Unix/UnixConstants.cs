namespace Acl.Fs.Native.PlatformSpecific.Unix;

internal static class UnixConstants
{
    internal static class Errors
    {
        internal const int EBadF = 9;
        internal const int EInval = 22;
    }

    internal static class FileAdvice
    {
        internal const int PosixFadvSequential = 2;
        internal const int PosixFadvDontNeed = 4;
    }

    internal static class IoPriority
    {
        internal const int SysSet = 251;
        internal const int ClassShift = 13;
        internal const int ClassRealTime = 1;
        internal const int ClassBestEffort = 2;
        internal const int WhoProcess = 1;
    }

    internal static class Libraries
    {
        internal const string LibcLibraryName = "libc";
    }
}