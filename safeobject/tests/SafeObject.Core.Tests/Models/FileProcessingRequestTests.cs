using System.Reflection;
using SafeObject.Core.Models;
using Xunit;

namespace SafeObject.Core.Tests.Models;

public class FileProcessingRequestTests
{
    private const string ValidFileId = "testFileId";

    private readonly string _validDestinationPath;
    private readonly string _validSourcePath;

    public FileProcessingRequestTests()
    {
        var tempPath = Path.GetTempPath();
        _validSourcePath = Path.Combine(tempPath, "source.txt");
        _validDestinationPath = Path.Combine(tempPath, "destination.txt");
    }

    private static bool InvokeValidatePath(string path)
    {
        var method = typeof(FileProcessingRequest)
            .GetMethod("ValidatePath", BindingFlags.NonPublic | BindingFlags.Static);
        var validatePath = method!.CreateDelegate<ValidatePathDelegate>();
        return validatePath(path.AsSpan());
    }

    private static bool InvokeHasValidRoot(string path)
    {
        var method = typeof(FileProcessingRequest)
            .GetMethod("HasValidRoot", BindingFlags.NonPublic | BindingFlags.Static);
        var hasValidRoot = method!.CreateDelegate<HasValidRootDelegate>();
        return hasValidRoot(path.AsSpan());
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        var request = new FileProcessingRequest(ValidFileId, _validSourcePath, _validDestinationPath);
        Assert.Equal(ValidFileId, request.FileId);
        Assert.Equal(_validSourcePath, request.SourcePath);
        Assert.Equal(_validDestinationPath, request.DestinationPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidFileId_ThrowsArgumentException(string? invalidFileId)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(invalidFileId!, _validSourcePath, _validDestinationPath));
        Assert.Equal("fileId", ex.ParamName, true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidSourcePath_ThrowsArgumentException(string? invalidSourcePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidSourcePath!, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidDestinationPath_ThrowsArgumentException(string? invalidDestinationPath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidDestinationPath!));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_InvalidSourcePath_FormatThrowsArgumentException()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "inva|id.txt");
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_InvalidDestinationPath_FormatThrowsArgumentException()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "inva|id.txt");
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidPath));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenSourcePathContainsInvalidCharacters()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), new string(Path.GetInvalidPathChars()[0], 1));
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenDestinationPathContainsInvalidCharacters()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), new string(Path.GetInvalidPathChars()[0], 1));
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidPath));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("file//txt")]
    public void Constructor_ShouldThrowArgumentException_WhenSourcePathIsInvalidFormat(string invalidSegment)
    {
        var tempPath = Path.GetTempPath();
        var invalidPath = $"{tempPath}{Path.DirectorySeparatorChar}{invalidSegment}";
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("file//txt")]
    public void Constructor_ShouldThrowArgumentException_WhenDestinationPathIsInvalidFormat(string invalidSegment)
    {
        var tempPath = Path.GetTempPath();
        var invalidPath = $"{tempPath}{Path.DirectorySeparatorChar}{invalidSegment}";
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidPath));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_PathWithAllInvalidChars_ThrowsArgumentException()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), new string(Path.GetInvalidPathChars()));
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("NUL")]
    public void Constructor_ReservedNameInSourcePath_ThrowsArgumentException(string reservedName)
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), reservedName);
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("NUL")]
    public void Constructor_ReservedNameInDestinationPath_ThrowsArgumentException(string reservedName)
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), reservedName);
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidPath));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_SourcePathEndingWithSpace_ThrowsArgumentException()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "file ");
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_DestinationPathEndingWithDot_ThrowsArgumentException()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "file.");
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, _validSourcePath, invalidPath));
        Assert.Equal("destinationPath", ex.ParamName, true);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("file//txt")]
    public void Constructor_PathWithDoubleSlashes_ThrowsArgumentException(string invalidSegment)
    {
        var tempPath = Path.GetTempPath();
        var invalidPath = $"{tempPath}{Path.DirectorySeparatorChar}{invalidSegment}";
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, invalidPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Fact]
    public void Constructor_SourcePathAsRootDirectory_ThrowsArgumentException()
    {
        var rootPath = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() =>
            new FileProcessingRequest(ValidFileId, rootPath, _validDestinationPath));
        Assert.Equal("sourcePath", ex.ParamName, true);
    }

    [Fact]
    public void ValidatePath_ValidPath_ReturnsTrue()
    {
        var validPath = Path.Combine(Path.GetTempPath(), "file.txt");
        var result = InvokeValidatePath(validPath);
        Assert.True(result);
    }

    [Fact]
    public void ValidatePath_EmptyOrWhitespace_ReturnsFalse()
    {
        Assert.False(InvokeValidatePath(""));
        Assert.False(InvokeValidatePath("   "));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("?")]
    [InlineData("\"")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("|")]
    public void ValidatePath_InvalidCharacters_ReturnsFalse(string invalidChar)
    {
        var path = Path.Combine(Path.GetTempPath(), $"file{invalidChar}.txt");
        var result = InvokeValidatePath(path);
        Assert.False(result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    public void ValidatePath_ReservedFileNames_ReturnsFalse(string reservedName)
    {
        var reservedPath = Path.Combine(Path.GetTempPath(), reservedName);
        var result = InvokeValidatePath(reservedPath);
        Assert.False(result);
    }

    [Theory]
    [InlineData("file ")]
    [InlineData("file.")]
    public void ValidatePath_TrailingSpaceOrPeriod_ReturnsFalse(string pathSegment)
    {
        var path = Path.Combine(Path.GetTempPath(), pathSegment);
        var result = InvokeValidatePath(path);
        Assert.False(result);
    }

    [Theory]
    [InlineData("file..txt")]
    [InlineData("file//txt")]
    public void ValidatePath_DoubleSlashes_ReturnsFalse(string pathSegment)
    {
        var tempPath = Path.GetTempPath();
        var path = $"{tempPath}{Path.DirectorySeparatorChar}{pathSegment}";
        var result = InvokeValidatePath(path);
        Assert.False(result);
    }

    [Fact]
    public void ValidatePath_RootDirectory_ReturnsFalse()
    {
        var root = Path.GetTempPath();
        var result = InvokeValidatePath(root);
        Assert.False(result);
    }

    [Fact]
    public void ValidatePath_WhenPathExceedsMaxLength_ReturnsFalse()
    {
        var longPath = Path.Combine(Path.GetTempPath(), new string('a', 261));
        var result = InvokeValidatePath(longPath);
        Assert.False(result);
    }

    [Fact]
    public void ValidatePath_WhenPathGetFullPathThrows_ReturnsFalse_Alternate()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "inva" + "\0" + "lid.txt");
        var result = InvokeValidatePath(invalidPath);
        Assert.False(result);
    }

    [Fact]
    public void HasValidRoot_PathLengthLessThan3_ReturnsFalse()
    {
        var result = InvokeHasValidRoot("A:");
        Assert.False(result);
    }

    [Fact]
    public void HasValidRoot_ValidDriveRoot_ReturnsTrue()
    {
        var result = InvokeHasValidRoot(Path.GetTempPath());
        Assert.True(result);
    }

    [Fact]
    public void HasValidRoot_UncPathWithBackslash_ReturnsTrue()
    {
        if (OperatingSystem.IsWindows())
        {
            var result = InvokeHasValidRoot("\\\\server\\share");
            Assert.True(result);
        }
    }

    [Fact]
    public void HasValidRoot_UncPathWithoutBackslash_ReturnsFalse()
    {
        if (OperatingSystem.IsWindows())
        {
            var result = InvokeHasValidRoot("\\\\ab");
            Assert.False(result);
        }
    }

    [Fact]
    public void HasValidRoot_NonRootPath_ReturnsFalse()
    {
        var result = InvokeHasValidRoot("abc");
        Assert.False(result);
    }

    private delegate bool ValidatePathDelegate(ReadOnlySpan<char> path);

    private delegate bool HasValidRootDelegate(ReadOnlySpan<char> path);
}