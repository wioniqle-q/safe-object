namespace Acl.Fs.Core.Versioning.Exceptions;

internal sealed class VersionValidationException : Exception
{
    internal VersionValidationException(string message) : base(message)
    {
    }

    internal VersionValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}