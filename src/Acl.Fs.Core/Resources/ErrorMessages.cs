namespace Acl.Fs.Core.Resources;

internal static class ErrorMessages
{
    internal const string FileIdCannotBeNullOrEmpty = "File ID cannot be null or empty.";
    internal const string SourcePathCannotBeNullOrInvalid = "Source path cannot be null or invalid.";
    internal const string DestinationPathCannotBeNullOrInvalid = "Destination path cannot be null or invalid.";

    internal const string InvalidKeySize = "Invalid key size. Key size must be 16, 24, or 32 bytes.";

    internal const string UnsupportedMajorVersion = "Unsupported major version: v{0}.{1}";
    internal const string VersionValidationFailed = "Version validation failed";
    internal const string MajorVersionCannotBeZero = "Major version cannot be 0";

    internal const string FileEncryptedWithNewerVersion =
        "File encrypted with newer version (v{0}.{1}) than supported (v{2}.{3})";
}