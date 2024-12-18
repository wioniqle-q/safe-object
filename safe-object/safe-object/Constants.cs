namespace safe_object;

public static class Constants
{
    public static class StorageConstants
    {
        public const int BufferSize = 81920;
        public const double Ratio = 1.618033988749895;
        public static readonly int[] PrimeNumbers = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29];
    }

    public static class KeyVaultConstants
    {
        public const int NonceSize = 12;
        public const int TagSize = 16;
    }

    public static class LinuxNativeConstants
    {
        public const int PosixFadvDontneed = 4;
        public const int PosixFadvSequential = 2;

        public const int IoprioWhoProcess = 1;
        public const int IoprioClassRt = 1;
        public const int IoprioClassBe = 2;

        public const int IoprioClassShift = 13;
        public const long SysIoprioSet = 251;
    }
}