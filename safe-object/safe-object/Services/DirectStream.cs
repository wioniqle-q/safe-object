using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;
using static safe_object.Kernel.LinuxKernel;
using static safe_object.Kernel.WindowsKernel;

namespace safe_object.Services;

public sealed class DirectStream : FileStream
{
    private const FileOptions DefaultFileOptions = FileOptions.WriteThrough;

    private readonly int _fileDescriptor;
    private readonly IntPtr _windowsHandle;
    private readonly ILogger<StorageService>? _logger;
    private readonly bool _isWindows;

    private volatile bool _disposed;
    private volatile int _isFlushInProgress;

    public DirectStream(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        ILogger<StorageService>? logger)
        : base(path, mode, access, share, bufferSize, options | DefaultFileOptions)
    {
        ArgumentNullException.ThrowIfNull(path);

        _logger = logger;
        _isWindows = OperatingSystem.IsWindows();

        if (_isWindows)
            _windowsHandle = SafeFileHandle.DangerousGetHandle();
        else
        {
            _fileDescriptor = SafeFileHandle.DangerousGetHandle().ToInt32();
            SetPriority();
            ApplyFadvise();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetPriority()
    {
        if (_isWindows)
            return;

        Thread.MemoryBarrier();

        var result = SetIoPriority(
            Constants.LinuxNativeConstants.IoprioWhoProcess,
            0,
            Constants.LinuxNativeConstants.IoprioClassRt,
            0
        );

        if (result is 0) return;

        result = SetIoPriority(
            Constants.LinuxNativeConstants.IoprioWhoProcess,
            0,
            Constants.LinuxNativeConstants.IoprioClassBe,
            0
        );

        if (result is not 0)
            _logger?.LogWarning("Failed to set I/O priority. Error: {errno}", Marshal.GetLastWin32Error());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyFadvise()
    {
        if (_isWindows)
            return;

        SecurityService.ProcessPaddingBuffer();

        var result = posix_fadvise(_fileDescriptor, 0, Length, Constants.LinuxNativeConstants.PosixFadvSequential);
        if (result is not 0)
            _logger?.LogWarning(
                $"posix_fadvise failed with result: {result}. File descriptor: {_fileDescriptor}, Length: {Length}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isFlushInProgress, 1) is 1)
            return;

        try
        {
            if (SecurityService.ValidateOperation() is not true)
            {
                SecurityService.ProcessPaddingBuffer();
                throw new SecurityException("Security validation failed");
            }

            ObjectDisposedException.ThrowIf(_disposed, nameof(DirectStream));

            Thread.SpinWait(Random.Shared.Next(10, 50));
            Thread.MemoryBarrier();

            await base.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (_isWindows && FlushFileBuffers(SafeFileHandle) is not true)
            {
                throw new IOException("FlushFileBuffers failed");
            }
            else
            {
                if (fsync(_fileDescriptor) is not 0)
                    throw new IOException($"fsync failed for file descriptor: {_fileDescriptor}.");

                SecurityService.ProcessPaddingBuffer();
                Thread.MemoryBarrier();

                var result = posix_fadvise(_fileDescriptor, 0, Length, Constants.LinuxNativeConstants.PosixFadvDontneed);
                if (result is not 0)
                    _logger?.LogWarning(
                        "posix_fadvise failed with result: {result}. File descriptor: {_fd}, Length: {Length}.",
                        result, _fileDescriptor, Length);
            }
        }
        finally
        {
            SecurityService.ProcessPaddingBuffer();
            Interlocked.Exchange(ref _isFlushInProgress, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing) base.Dispose(disposing);

            if (_isWindows)
                CloseHandle(_windowsHandle);
            else
                close(_fileDescriptor);
        }
        finally
        {
            _disposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
            
            if (_isWindows)
                CloseHandle(_windowsHandle);
            else
                close(_fileDescriptor);
        }
        finally
        {
            _disposed = true;
        }
    }

    ~DirectStream()
    {
        Dispose(true);
    }
}
