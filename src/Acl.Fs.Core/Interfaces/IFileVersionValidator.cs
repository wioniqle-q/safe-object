namespace Acl.Fs.Core.Interfaces;

internal interface IFileVersionValidator
{
    void ValidateVersion(byte majorVersion, byte minorVersion);
}