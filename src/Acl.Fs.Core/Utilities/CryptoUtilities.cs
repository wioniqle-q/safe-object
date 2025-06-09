using System.Runtime.CompilerServices;
using Acl.Fs.Core.Resources;
using Acl.Fs.Stream.Core;
using Microsoft.Extensions.Logging;
using static Acl.Fs.Abstractions.Constants.StorageConstants;

namespace Acl.Fs.Core.Utilities;

internal static class CryptoUtilities
{
    private static FileOptions FileOptions => FileOptions.Asynchronous | FileOptions.SequentialScan |
                                              FileOptions.WriteThrough | (FileOptions)0x20000000;

    internal static System.IO.Stream CreateInputStream(string path, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, ErrorMessages.SourcePathCannotBeNullOrInvalid);

        return DirectStreamFactory.Create(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions,
            logger);
    }

    internal static System.IO.Stream CreateOutputStream(string path, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, ErrorMessages.DestinationPathCannotBeNullOrInvalid);

        return DirectStreamFactory.Create(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions,
            logger);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CalculateAlignedSize(int bytesRead, bool isLastBlock)
    {
        if (isLastBlock is not true) return bytesRead;

        return (bytesRead + SectorSize - 1) & ~(SectorSize - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CalculateAlignedSize(int bytesRead)
    {
        return (bytesRead + SectorSize - 1) & ~(SectorSize - 1);
    }
}