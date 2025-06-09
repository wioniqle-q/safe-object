using System.Runtime.InteropServices;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Resources;

namespace Acl.Fs.Core.UnitTests.Models;

public sealed class FileTransferInstructionTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        const string fileId = "test-file-id";

        var sourcePath = GetValidPathForCurrentOs();
        var destinationPath = GetValidPathForCurrentOs("destination");

        var instruction = new FileTransferInstruction(fileId, sourcePath, destinationPath);

        Assert.Equal(fileId, instruction.FileId);
        Assert.Equal(sourcePath, instruction.SourcePath);
        Assert.Equal(destinationPath, instruction.DestinationPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_InvalidFileId_ThrowsArgumentException(string invalidFileId)
    {
        var sourcePath = GetValidPathForCurrentOs();
        var destinationPath = GetValidPathForCurrentOs("destination");

        var exception = Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(invalidFileId, sourcePath, destinationPath));

        Assert.Contains(ErrorMessages.FileIdCannotBeNullOrEmpty, exception.Message);
        Assert.Equal("FileId", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_InvalidSourcePath_ThrowsArgumentException(string invalidPath)
    {
        const string fileId = "test-file-id";

        var destinationPath = GetValidPathForCurrentOs("destination");

        var exception = Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, invalidPath, destinationPath));

        Assert.Contains(ErrorMessages.SourcePathCannotBeNullOrInvalid, exception.Message);
        Assert.Equal("SourcePath", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_InvalidDestinationPath_ThrowsArgumentException(string invalidPath)
    {
        const string fileId = "test-file-id";
        var sourcePath = GetValidPathForCurrentOs();

        var exception = Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, sourcePath, invalidPath));

        Assert.Contains(ErrorMessages.DestinationPathCannotBeNullOrInvalid, exception.Message);
        Assert.Equal("DestinationPath", exception.ParamName);
    }

    [SkippableTheory]
    [InlineData(@"C:\source\file*.txt")]
    [InlineData(@"C:\source\file?.txt")]
    [InlineData(@"C:\source\file"".txt")]
    [InlineData(@"C:\source\file<.txt")]
    [InlineData(@"C:\source\file>.txt")]
    [InlineData(@"C:\source\file|.txt")]
    public void Constructor_PathWithInvalidCharacters_ThrowsArgumentException(string pathWithInvalidChars)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathWithInvalidChars, validPath));
    }

    [SkippableTheory]
    [InlineData(@"C:\source\")]
    [InlineData(@"C:\source\ ")]
    [InlineData(@"C:\source\folder\")]
    public void Constructor_PathEndingWithDirectorySeparator_ThrowsArgumentException(string pathEndingWithSeparator)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathEndingWithSeparator, validPath));
    }

    [SkippableTheory]
    [InlineData(@"C:\source\file ")]
    [InlineData(@"C:\source\file.")]
    public void Constructor_PathEndingWithSpaceOrDot_ThrowsArgumentException(string pathEndingIncorrectly)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathEndingIncorrectly, validPath));
    }

    [SkippableTheory]
    [InlineData(@"C:\source\\file.txt")]
    [InlineData(@"C://source//file.txt")]
    [InlineData(@"C:\source\\\file.txt")]
    public void Constructor_PathWithDoubleSlash_ThrowsArgumentException(string pathWithDoubleSlash)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathWithDoubleSlash, validPath));
    }

    [SkippableTheory]
    [InlineData(@"C:\source\..\file.txt")]
    [InlineData(@"C:\source\folder\..\file.txt")]
    [InlineData(@"C:\source\../file.txt")]
    [InlineData(@"C:/source/../file.txt")]
    public void Constructor_PathWithParentDirectoryReference_ThrowsArgumentException(string pathWithParentRef)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathWithParentRef, validPath));
    }

    [SkippableTheory]
    [InlineData(@"C:\source\CON")]
    [InlineData(@"C:\source\PRN.txt")]
    [InlineData(@"C:\source\AUX.log")]
    [InlineData(@"C:\source\NUL")]
    [InlineData(@"C:\source\COM1.dat")]
    [InlineData(@"C:\source\LPT1")]
    public void Constructor_PathWithReservedNames_ThrowsArgumentException(string pathWithReservedName)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, pathWithReservedName, validPath));
    }

    [SkippableFact]
    public void Constructor_PathTooLong_ThrowsArgumentException()
    {
        const string fileId = "test-file-id";
        var longPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\" + new string('a', 300) + ".txt"
            : "/" + new string('a', 300) + ".txt";
        var validPath = GetValidPathForCurrentOs("destination");

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, longPath, validPath));
    }

    [SkippableFact]
    public void Constructor_ValidWindowsPaths_CreatesInstance()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        const string fileId = "test-file-id";
        const string destinationPath = @"C:\destination\file.txt";

        var validPaths = new[]
        {
            @"C:\source\file.txt",
            @"C:\source\folder\file.txt",
            @"C:\source\folder\subfolder\file.txt"
        };

        foreach (var validPath in validPaths)
        {
            var instruction = new FileTransferInstruction(fileId, validPath, destinationPath);

            Assert.NotNull(instruction);
            Assert.Equal(validPath, instruction.SourcePath);
        }
    }

    [SkippableFact]
    public void Constructor_ValidUnixPath_CreatesInstance()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

        const string fileId = "test-file-id";
        const string sourcePath = "/home/user/file.txt";
        const string destinationPath = "/tmp/file.txt";

        var instruction = new FileTransferInstruction(fileId, sourcePath, destinationPath);

        Assert.NotNull(instruction);
        Assert.Equal(sourcePath, instruction.SourcePath);
        Assert.Equal(destinationPath, instruction.DestinationPath);
    }

    [Fact]
    public void FileTransferInstruction_IsRecord_SupportsValueEquality()
    {
        const string fileId = "test-file-id";

        var sourcePath = GetValidPathForCurrentOs();
        var destinationPath = GetValidPathForCurrentOs("destination");

        var instruction1 = new FileTransferInstruction(fileId, sourcePath, destinationPath);
        var instruction2 = new FileTransferInstruction(fileId, sourcePath, destinationPath);

        Assert.Equal(instruction1, instruction2);
        Assert.True(instruction1 == instruction2);
        Assert.Equal(instruction1.GetHashCode(), instruction2.GetHashCode());
    }

    [Fact]
    public void FileTransferInstruction_DifferentValues_NotEqual()
    {
        var sourcePath = GetValidPathForCurrentOs();
        var destinationPath = GetValidPathForCurrentOs("destination");

        var instruction1 = new FileTransferInstruction("id1", sourcePath, destinationPath);
        var instruction2 = new FileTransferInstruction("id2", sourcePath, destinationPath);

        Assert.NotEqual(instruction1, instruction2);
        Assert.False(instruction1 == instruction2);
    }

    [SkippableFact]
    public void Constructor_InvalidUNCPaths_ThrowsArgumentException()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "UNC paths are Windows-specific");

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        var invalidUncPaths = new[]
        {
            @"\\server\file.txt",
            @"\\server\\file.txt",
            @"\\\file.txt",
            @"\\file.txt"
        };

        foreach (var invalidPath in invalidUncPaths)
            Assert.Throws<ArgumentException>(() =>
                new FileTransferInstruction(fileId, invalidPath, validPath));
    }

    [SkippableFact]
    public void Constructor_LinuxPaths_CreatesInstance()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Linux paths test");

        const string fileId = "test-file-id";

        var validLinuxPaths = new[]
        {
            ("/home/user/file.txt", "/tmp/file.txt"),
            ("/var/log/app.log", "/backup/app.log"),
            ("/opt/app/config.json", "/etc/app/config.json")
        };

        foreach (var (sourcePath, destinationPath) in validLinuxPaths)
        {
            var instruction = new FileTransferInstruction(fileId, sourcePath, destinationPath);

            Assert.NotNull(instruction);
            Assert.Equal(sourcePath, instruction.SourcePath);
            Assert.Equal(destinationPath, instruction.DestinationPath);
        }
    }

    [SkippableFact]
    public void Constructor_MacOSPaths_CreatesInstance()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "macOS paths test");

        const string fileId = "test-file-id";

        var validMacPaths = new[]
        {
            ("/Users/user/Documents/file.txt", "/tmp/file.txt"),
            ("/Applications/App.app/Contents/file.plist", "/Library/file.plist"),
            ("/System/Library/file.dylib", "/usr/local/lib/file.dylib")
        };

        foreach (var (sourcePath, destinationPath) in validMacPaths)
        {
            var instruction = new FileTransferInstruction(fileId, sourcePath, destinationPath);

            Assert.NotNull(instruction);
            Assert.Equal(sourcePath, instruction.SourcePath);
            Assert.Equal(destinationPath, instruction.DestinationPath);
        }
    }

    [SkippableTheory]
    [InlineData(@"relative\path\file.txt")]
    [InlineData("relative/path/file.txt")]
    [InlineData("file.txt")]
    [InlineData("folder\\file.txt")]
    public void Constructor_RelativePaths_ThrowsArgumentException(string relativePath)
    {
        const string fileId = "test-file-id";
        var validPath = GetValidPathForCurrentOs("destination");

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, relativePath, validPath));
    }

    [SkippableTheory]
    [InlineData("D:file.txt")]
    [InlineData("Z:folder\\file.txt")]
    public void Constructor_InvalidWindowsRoots_ThrowsArgumentException(string invalidPath)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Windows-specific path validation");

        const string fileId = "test-file-id";
        const string validPath = @"C:\destination\file.txt";

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, invalidPath, validPath));
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        const string fileId = "test-file-id";
        var validPath = GetValidPathForCurrentOs("destination");

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, "", validPath));

        Assert.Throws<ArgumentException>(() =>
            new FileTransferInstruction(fileId, validPath, ""));
    }

    private static string GetValidPathForCurrentOs(string? subdirectory = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return subdirectory != null
                ? $@"C:\{subdirectory}\file.txt"
                : @"C:\source\file.txt";

        return subdirectory != null
            ? $"/{subdirectory}/file.txt"
            : "/source/file.txt";
    }
}