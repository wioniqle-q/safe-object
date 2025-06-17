using Acl.Fs.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.Core.UnitTests.Utilities;

public sealed class CryptoUtilitiesTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles;

    public CryptoUtilitiesTests()
    {
        _logger = new TestLogger();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempFiles = [];
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in _tempFiles.Where(File.Exists)) File.Delete(file);

            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void CreateInputStream_ValidFile_ReturnsDirectStream()
    {
        var tempFile = CreateTempFile("test content");


        using var stream = CryptoUtilities.CreateInputStream(tempFile, _logger);


        Assert.NotNull(stream);
        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.Contains("DirectStream", stream.GetType().Name);
    }

    [Fact]
    public void CreateInputStream_ValidFile_CanReadContent()
    {
        const string testContent = "Hello, CryptoUtilities!";

        var tempFile = CreateTempFile(testContent);

        using var stream = CryptoUtilities.CreateInputStream(tempFile, _logger);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();


        Assert.Equal(testContent, content);
    }

    [Fact]
    public void CreateInputStream_NonExistentFile_ThrowsException()
    {
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.txt");

        Assert.ThrowsAny<Exception>(() => CryptoUtilities.CreateInputStream(nonExistentPath, _logger));
    }

    [Fact]
    public void CreateOutputStream_ValidPath_ReturnsDirectStream()
    {
        var outputPath = GetTempFilePath();

        using var stream = CryptoUtilities.CreateOutputStream(outputPath, _logger);

        Assert.NotNull(stream);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.Contains("DirectStream", stream.GetType().Name);
    }

    [Fact]
    public void CreateOutputStream_ValidPath_CreatesFile()
    {
        var outputPath = GetTempFilePath();

        using (var _ = CryptoUtilities.CreateOutputStream(outputPath, _logger))
        {
        }

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void CreateOutputStream_ExistingFile_OverwritesFile()
    {
        var outputPath = CreateTempFile("original content");
        var originalLength = new FileInfo(outputPath).Length;

        using (CryptoUtilities.CreateOutputStream(outputPath, _logger))
        {
        }

        var newLength = new FileInfo(outputPath).Length;
        Assert.True(newLength <= originalLength);
    }

    [Fact]
    public void CreateInputStream_WithLogger_DoesNotThrow()
    {
        var tempFile = CreateTempFile("test");

        var exception = Record.Exception(() =>
        {
            using var stream = CryptoUtilities.CreateInputStream(tempFile, _logger);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CreateOutputStream_WithLogger_DoesNotThrow()
    {
        var outputPath = GetTempFilePath();

        var exception = Record.Exception(() =>
        {
            using var stream = CryptoUtilities.CreateOutputStream(outputPath, _logger);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CreateInputStream_LargeFile_HandlesCorrectly()
    {
        var largeContent = new string('X', 5000);
        var tempFile = CreateTempFile(largeContent);


        using var stream = CryptoUtilities.CreateInputStream(tempFile, _logger);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();


        Assert.Equal(largeContent.Length, content.Length);
        Assert.Equal(largeContent, content);
    }

    [Fact]
    public void CreateInputStream_UsesCorrectFileMode()
    {
        var tempFile = CreateTempFile("test");

        using var stream = CryptoUtilities.CreateInputStream(tempFile, _logger);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void CreateOutputStream_UsesCorrectFileMode()
    {
        var outputPath = GetTempFilePath();

        using var stream = CryptoUtilities.CreateOutputStream(outputPath, _logger);

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void CreateInputStream_MultipleCallsSameFile_AllowsSharedRead()
    {
        var tempFile = CreateTempFile("shared content");

        using var stream1 = CryptoUtilities.CreateInputStream(tempFile, _logger);
        using var stream2 = CryptoUtilities.CreateInputStream(tempFile, _logger);

        Assert.NotNull(stream1);
        Assert.NotNull(stream2);
        Assert.True(stream1.CanRead);
        Assert.True(stream2.CanRead);
    }

    [Fact]
    public void CreateOutputStream_MultipleCallsSameFile_SecondCallFails()
    {
        var outputPath = GetTempFilePath();

        using var stream1 = CryptoUtilities.CreateOutputStream(outputPath, _logger);

        Assert.ThrowsAny<Exception>(() => CryptoUtilities.CreateOutputStream(outputPath, _logger));
    }

    [Fact]
    public void CalculateAlignedSize_WithIsLastBlock_True_ReturnsAlignedSize()
    {
        const int bytesRead = 300;
        const bool isLastBlock = true;
        const int expectedAligned = 512;

        var result = CryptoUtilities.CalculateAlignedSize(bytesRead, isLastBlock);

        Assert.Equal(expectedAligned, result);
    }

    [Fact]
    public void CalculateAlignedSize_WithIsLastBlock_False_ReturnsOriginalSize()
    {
        const int bytesRead = 300;
        const bool isLastBlock = false;

        var result = CryptoUtilities.CalculateAlignedSize(bytesRead, isLastBlock);

        Assert.Equal(bytesRead, result);
    }

    [Fact]
    public void CalculateAlignedSize_WithIsLastBlock_AlreadyAligned_ReturnsOriginalSize()
    {
        const int bytesRead = 512;
        const bool isLastBlock = true;

        var result = CryptoUtilities.CalculateAlignedSize(bytesRead, isLastBlock);

        Assert.Equal(bytesRead, result);
    }

    [Fact]
    public void CalculateAlignedSize_SingleParameter_ReturnsAlignedSize()
    {
        const int bytesRead = 300;
        const int expectedAligned = 512;

        var result = CryptoUtilities.CalculateAlignedSize(bytesRead);

        Assert.Equal(expectedAligned, result);
    }

    [Fact]
    public void CalculateAlignedSize_SingleParameter_AlreadyAligned_ReturnsOriginalSize()
    {
        const int bytesRead = 512;

        var result = CryptoUtilities.CalculateAlignedSize(bytesRead);

        Assert.Equal(bytesRead, result);
    }

    [Theory]
    [InlineData(1, 512)]
    [InlineData(100, 512)]
    [InlineData(511, 512)]
    [InlineData(512, 512)]
    [InlineData(513, 1024)]
    [InlineData(1000, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(1025, 1536)]
    public void CalculateAlignedSize_SingleParameter_VariousInputs_ReturnsCorrectAlignment(int input, int expected)
    {
        var result = CryptoUtilities.CalculateAlignedSize(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, true, 512)]
    [InlineData(100, true, 512)]
    [InlineData(511, true, 512)]
    [InlineData(512, true, 512)]
    [InlineData(513, true, 1024)]
    [InlineData(1000, false, 1000)]
    [InlineData(1024, false, 1024)]
    [InlineData(1025, false, 1025)]
    public void CalculateAlignedSize_WithIsLastBlock_VariousInputs_ReturnsCorrectResult(int input, bool isLastBlock,
        int expected)
    {
        var result = CryptoUtilities.CalculateAlignedSize(input, isLastBlock);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateAlignedSize_Zero_ReturnsZero()
    {
        Assert.Equal(0, CryptoUtilities.CalculateAlignedSize(0));
        Assert.Equal(0, CryptoUtilities.CalculateAlignedSize(0, true));
        Assert.Equal(0, CryptoUtilities.CalculateAlignedSize(0, false));
    }

    private string CreateTempFile(string content = "")
    {
        var tempFile = GetTempFilePath();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private string GetTempFilePath()
    {
        var tempFile = Path.Combine(_tempDirectory, Guid.NewGuid() + ".txt");
        _tempFiles.Add(tempFile);
        return tempFile;
    }

    private class TestLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}