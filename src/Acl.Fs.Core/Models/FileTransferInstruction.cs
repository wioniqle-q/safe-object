using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Acl.Fs.Core.Resources;

namespace Acl.Fs.Core.Models;

public sealed partial record FileTransferInstruction
{
    private static readonly FrozenSet<char> InvalidPathChars =
        Path.GetInvalidPathChars().Concat(['*', '?', '"', '<', '>', '|']).ToFrozenSet();

    private static readonly FrozenSet<string> ReservedNames =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4",
            "COM5", "COM6", "COM7", "COM8", "COM9", "COM^", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
            "LPT8", "LPT9");

    private static readonly Regex ParentDirectoryRegex = DirectoryRegex();

    public FileTransferInstruction(string fileId, string sourcePath, string destinationPath)
    {
        FileId = fileId;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;

        Validate();
    }

    public string FileId { get; }
    public string SourcePath { get; }
    public string DestinationPath { get; }

    private void Validate()
    {
        if (ValidateString(FileId.AsSpan()) is not true)
            throw new ArgumentException(ErrorMessages.FileIdCannotBeNullOrEmpty, nameof(FileId));
        if (ValidatePath(SourcePath.AsSpan()) is not true)
            throw new ArgumentException(ErrorMessages.SourcePathCannotBeNullOrInvalid, nameof(SourcePath));
        if (ValidatePath(DestinationPath.AsSpan()) is not true)
            throw new ArgumentException(ErrorMessages.DestinationPathCannotBeNullOrInvalid, nameof(DestinationPath));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateString(ReadOnlySpan<char> value)
    {
        return value.IsEmpty is not true && value.IsWhiteSpace() is not true;
    }

    private static bool ValidatePath(ReadOnlySpan<char> value)
    {
        if (ValidateString(value) is not true || value.Length > 260 || HasDoubleSlash(value) ||
            value.EndsWith(" ") || value.EndsWith("."))
            return false;

        foreach (var t in value)
            if (InvalidPathChars.Contains(t))
                return false;

        var lastSeparatorIndex = value.LastIndexOfAny(Path.DirectorySeparatorChar, '/');

        var fileNameSpan = lastSeparatorIndex >= 0 ? value[(lastSeparatorIndex + 1)..] : value;
        if (fileNameSpan.IsEmpty || fileNameSpan.IsWhiteSpace())
            return false;

        if (ContainsParentDirectoryReference(value))
            return false;

        Span<char> fileNameUpper = stackalloc char[fileNameSpan.Length];
        fileNameSpan.ToUpperInvariant(fileNameUpper);

        if (ReservedNames.Contains(fileNameUpper.ToString()))
            return false;

        var extensionIndex = fileNameSpan.LastIndexOf('.');
        if (extensionIndex <= 0) return HasValidRoot(value);

        var fileNameWithoutExt = fileNameSpan[..extensionIndex];

        Span<char> fileNameWithoutExtUpper = stackalloc char[fileNameWithoutExt.Length];
        fileNameWithoutExt.ToUpperInvariant(fileNameWithoutExtUpper);

        return ReservedNames.Contains(fileNameWithoutExtUpper.ToString()) is not true && HasValidRoot(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasDoubleSlash(ReadOnlySpan<char> path)
    {
        if (path.Length < 2)
            return false;

        var prev = path[0];

        for (var i = 1; i < path.Length; i++)
        {
            var current = path[i];
            if ((current is '/' && prev is '/') ||
                (current == Path.DirectorySeparatorChar && prev == Path.DirectorySeparatorChar))
                return true;

            prev = current;
        }

        return false;
    }

    private static bool ContainsParentDirectoryReference(ReadOnlySpan<char> path)
    {
        return ParentDirectoryRegex.IsMatch(path);
    }

    private static bool HasValidRoot(ReadOnlySpan<char> path)
    {
        if (path.Length < 1)
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            switch (path.Length)
            {
                case >= 3 when path[1] is ':' && path[2] == Path.DirectorySeparatorChar:
                    return true;
                case > 3 when path[0] is '\\' && path[1] is '\\':
                    return path[2..].IndexOf(Path.DirectorySeparatorChar) >= 0;
            }
        }
        else
        {
            if (path[0] == Path.DirectorySeparatorChar)
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"(?:^|[\\/])(\.\.)(?:[\\/]|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DirectoryRegex();
}