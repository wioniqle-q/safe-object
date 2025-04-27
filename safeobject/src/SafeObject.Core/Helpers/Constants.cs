using System.Collections.Immutable;

namespace SafeObject.Core.Helpers;

public static class Constants
{
    public static class Storage
    {
        public const int BufferSize = 81920;
    }

    public static class Security
    {
        public static class KeyVault
        {
            public const int NonceSize = 12;
            public const int TagSize = 16;
            public const int HmacKeySize = 32;
            public const int SaltSize = 32;
            public const int DefaultKeySize = 32;
            public static readonly ImmutableArray<byte> NonceContext = [.."AES-GCM-NONCE-V1"u8.ToArray()];
        }
    }

    public static class EncryptionKey
    {
        public const string FileIdNullErrorMessage = "File ID cannot be null, empty, or whitespace.";

        public const string EncryptedFilePrivateKeyNullErrorMessage =
            "Encrypted file private key cannot be null or empty.";
    }

    public static class FileProcessing
    {
        public const string FileIdNullErrorMessage = "FileId cannot be null or empty.";
        public const string SourcePathNullErrorMessage = "SourcePath is invalid or empty.";
        public const string DestinationPathNullErrorMessage = "DestinationPath is invalid or empty.";
    }

    public static class Windows
    {
        public const string Kernel32LibraryName = "kernel32.dll";
    }

    public static class MacOs
    {
        public const string LibcLibraryName = "libc";
        
        public const int FFullfsync = 51;
        public const int HmacKeySize = 32;
        public const int SaltSize = 32;
    }

    public static class Unix
    {
        public const string LibcLibraryName = "libc";

        public static class FileAdvice
        {
            public const int DontNeed = 4;
            public const int Sequential = 2;
        }

        public static class Errors
        {
            public const int EBadF = 9;
            public const int EInval = 22;
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
}
