namespace Acl.Fs.Core.Interfaces;

internal interface IVersionValidationStrategy
{
    void Validate(byte minorVersion);
}