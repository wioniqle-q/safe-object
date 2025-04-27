using System.Collections.Immutable;
using System.Runtime.InteropServices;
using SafeObject.Core.Helpers;

namespace SafeObject.Core.Models;

public sealed record FileProcessingRequest
{
    private static readonly ImmutableHashSet<char> InvalidPathChars =
        Path.GetInvalidPathChars().Concat(['*', '?', '"', '<', '>', '|']).ToImmutableHashSet();

    private static readonly ImmutableHashSet<string> ReservedNames =
        ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "COM^",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            "LPT^");

    public FileProcessingRequest(string fileId, string sourcePath, string destinationPath)
    {
        ValidateParameters([
            (nameof(fileId), fileId, ValidateString, Constants.FileProcessing.FileIdNullErrorMessage),
            (nameof(sourcePath), sourcePath, ValidatePath, Constants.FileProcessing.SourcePathNullErrorMessage),
            (nameof(destinationPath), destinationPath, ValidatePath,
                Constants.FileProcessing.DestinationPathNullErrorMessage)
        ]);
        FileId = fileId;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
    }

    public string FileId { get; }
    public string SourcePath { get; }
    public string DestinationPath { get; }

    private static void ValidateParameters(
        (string Name, string Value, Func<ReadOnlySpan<char>, bool> Validator, string ErrorMessage)[] parameters)
    {
        foreach (var (name, value, validator, errorMessage) in parameters)
            if (validator(value.AsSpan()) is not true)
                throw new ArgumentException(errorMessage, name);
    }

    private static bool ValidateString(ReadOnlySpan<char> value)
    {
        return value.IsEmpty is not true && value.IsWhiteSpace() is not true;
    }

    private static bool ValidatePath(ReadOnlySpan<char> value)
    {
        if (ValidateString(value) is not true || value.Length > 260 || HasDoubleSlash(value) ||
            value.EndsWith(" ") || value.EndsWith("."))
            return false;

        foreach (var c in value)
            if (InvalidPathChars.Contains(c))
                return false;

        var valueStr = value.ToString();
        var fileName = Path.GetFileName(valueStr);
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var segments = valueStr.Split([Path.DirectorySeparatorChar, '/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is ".."))
            return false;

        var fileNameUpper = fileName.ToUpperInvariant();
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(valueStr).ToUpperInvariant();
        if (ReservedNames.Contains(fileNameUpper) || ReservedNames.Contains(fileNameWithoutExt))
            return false;

        return HasValidRoot(value);
    }

    private static bool HasDoubleSlash(ReadOnlySpan<char> path)
    {
        for (var i = 1; i < path.Length; i++)
            if ((path[i] is '/' && path[i - 1] is '/') ||
                (path[i] == Path.DirectorySeparatorChar && path[i - 1] == Path.DirectorySeparatorChar))
                return true;

        return false;
    }

    private static bool HasValidRoot(ReadOnlySpan<char> path)
    {
        if (path.Length < 1)
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (path.Length >= 3 && path[1] is ':' && path[2] == Path.DirectorySeparatorChar)
                return true;

            if (path.StartsWith(@"\\") && path.Length > 3)
                return path[2..].Contains(Path.DirectorySeparatorChar);
        }
        else
        {
            if (path[0] == Path.DirectorySeparatorChar)
                return true;
        }

        return false;
    }
}