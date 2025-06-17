namespace Acl.Fs.Abstractions.Constants;

internal static class KeyVaultConstants
{
    internal const int NonceSize = 12;
    internal const int TagSize = 16;

    internal static readonly byte[] NonceContext =
        [0x41, 0x43, 0x4C, 0x5F, 0x4E, 0x4F, 0x4E, 0x43, 0x45];

    internal static int HmacKeySize => IsRunningOnGitHubActions ? 32 : 64;

    internal static int SaltSize => IsRunningOnGitHubActions ? 32 : 64;

    internal static bool IsRunningOnGitHubActions =>
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is "true" ||
        Environment.GetEnvironmentVariable("CI") is "true";
}