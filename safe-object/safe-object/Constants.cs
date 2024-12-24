namespace safe_object;

public static class Constants
{
    public static class Storage
    {
        public const int BufferSize = 81920;
        public const double GoldenRatio = 1.618033988749895;
        public static ReadOnlySpan<int> PrimeNumbers => [2, 3, 5, 7, 11, 13, 17, 19, 23, 29];
    }

    public static class Security
    {
        public static class KeyVault
        {
            public const int NonceSize = 12;
            public const int TagSize = 16;
        }
    }

    public static class Linux
    {
        public static class FileAdvice
        {
            public const int DontNeed = 4;
            public const int Sequential = 2;
        }

        public static class IoPriority
        {
            public const int WhoProcess = 1;
            public const int ClassRealTime = 1;
            public const int ClassBestEffort = 2;
            public const int ClassShift = 13;
            public const long SysSet = 251;
        }
    }

    public static class DirectStream
    {
        public static class SpinWait
        {
            public const int MinDuration = 10;
            public const int MaxDuration = 50;
        }
    }
}