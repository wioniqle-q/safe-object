using Acl.Fs.Core.Interfaces;

namespace Acl.Fs.Core.Versioning.ValidationStrategies;

internal sealed class V1ValidationStrategy : IVersionValidationStrategy
{
    public void Validate(byte minorVersion)
    {
    }
}