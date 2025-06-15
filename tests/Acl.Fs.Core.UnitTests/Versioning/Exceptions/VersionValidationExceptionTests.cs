using Acl.Fs.Core.Versioning.Exceptions;

namespace Acl.Fs.Core.UnitTests.Versioning.Exceptions;

public sealed class VersionValidationExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        const string expectedMessage = "Test version validation error";

        var exception = new VersionValidationException(expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        const string expectedMessage = "Test version validation error";
        var innerException = new InvalidOperationException("Inner error");

        var exception = new VersionValidationException(expectedMessage, innerException);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_WithNullMessage_HandlesGracefully()
    {
        var exception = new VersionValidationException(null!);

        Assert.NotNull(exception);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithEmptyMessage_SetsEmptyMessage()
    {
        const string emptyMessage = "";

        var exception = new VersionValidationException(emptyMessage);

        Assert.Equal(emptyMessage, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithNullInnerException_HandlesGracefully()
    {
        const string message = "Test message";

        var exception = new VersionValidationException(message, null!);

        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void VersionValidationException_IsException()
    {
        var exception = new VersionValidationException("test");

        Assert.IsType<Exception>(exception, false);
    }

    [Fact]
    public void VersionValidationException_CanBeThrown()
    {
        const string expectedMessage = "Test exception throwing";

        var exception = Assert.Throws<VersionValidationException>((Action)(() =>
            throw new VersionValidationException(expectedMessage)));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void VersionValidationException_CanBeCaught()
    {
        const string expectedMessage = "Test exception catching";
        VersionValidationException? caughtException;

        try
        {
            throw new VersionValidationException(expectedMessage);
        }
        catch (VersionValidationException ex)
        {
            caughtException = ex;
        }

        Assert.NotNull(caughtException);
        Assert.Equal(expectedMessage, caughtException.Message);
    }

    [Fact]
    public void VersionValidationException_PreservesStackTrace()
    {
        VersionValidationException? exception = null;

        try
        {
            ThrowVersionValidationException();
        }
        catch (VersionValidationException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.NotNull(exception.StackTrace);
        Assert.Contains(nameof(ThrowVersionValidationException), exception.StackTrace);
    }

    [Fact]
    public void VersionValidationException_WithInnerException_PreservesInnerStackTrace()
    {
        const string outerMessage = "Outer exception";

        var innerException = new InvalidOperationException("Inner exception");
        var exception = new VersionValidationException(outerMessage, innerException);

        Assert.Equal(outerMessage, exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.NotNull(exception.InnerException);
        Assert.Equal("Inner exception", exception.InnerException.Message);
    }

    private static void ThrowVersionValidationException()
    {
        throw new VersionValidationException("Test exception for stack trace");
    }
}