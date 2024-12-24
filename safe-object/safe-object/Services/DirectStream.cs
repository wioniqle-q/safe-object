using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;
using static safe_object.Kernel.LinuxKernel;
using static safe_object.Kernel.WindowsKernel;

namespace safe_object.Services;

public sealed class DirectStream : FileStream
{
    private const FileOptions DefaultOptions = FileOptions.WriteThrough;

    private readonly NativeHandles _handles;
    private readonly bool _isWindows;
    private readonly ILogger<StorageService>? _logger;
    private bool _disposed;

    private int _flushState;

    public DirectStream(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        ILogger<StorageService>? logger)
        : base(path ?? throw new ArgumentNullException(nameof(path)),
            mode,
            access,
            share,
            bufferSize,
            options | DefaultOptions)
    {
        _logger = logger;
        _isWindows = OperatingSystem.IsWindows();
        _handles = InitializeNativeHandles();

        ConfigureStreamProperties();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NativeHandles InitializeNativeHandles()
    {
        return _isWindows
            ? new NativeHandles(0, SafeFileHandle.DangerousGetHandle())
            : new NativeHandles(SafeFileHandle.DangerousGetHandle().ToInt32(), IntPtr.Zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureStreamProperties()
    {
        if (_isWindows is not true) ConfigureUnixStreamProperties();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureUnixStreamProperties()
    {
        SetUnixPriority();
        ConfigureUnixAdvice();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetUnixPriority()
    {
        Thread.MemoryBarrier();

        if (TrySetIoPriority(Constants.Linux.IoPriority.ClassRealTime) ||
            TrySetIoPriority(Constants.Linux.IoPriority.ClassBestEffort))
            return;

        _logger?.LogWarning("Failed to set I/O priority. Error: {errno}", Marshal.GetLastWin32Error());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySetIoPriority(int priorityClass)
    {
        return SetIoPriority(
            Constants.Linux.IoPriority.WhoProcess,
            0,
            priorityClass,
            0) is 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureUnixAdvice()
    {
        SecurityService.ProcessPaddingBuffer();

        var result = posix_fadvise(
            _handles.FileDescriptor,
            0,
            Length,
            Constants.Linux.FileAdvice.Sequential);

        if (result is not 0)
            _logger?.LogWarning(
                "posix_fadvise failed with result: {Result}. File descriptor: {FileDescriptor}, Length: {Length}",
                result,
                _handles.FileDescriptor,
                Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (TryEnterFlushOperation() is not true) return;

        try
        {
            await ExecuteFlushOperationAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CompleteFlushOperation();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryEnterFlushOperation()
    {
        return Interlocked.Exchange(ref _flushState, 1) is not 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task ExecuteFlushOperationAsync(CancellationToken cancellationToken)
    {
        ValidateFlushOperation();

        await ExecutePlatformSpecificFlushAsync(cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateFlushOperation()
    {
        if (SecurityService.ValidateOperation() is not true)
        {
            SecurityService.ProcessPaddingBuffer();
            throw new SecurityException("Security validation failed");
        }

        ObjectDisposedException.ThrowIf(_disposed, nameof(DirectStream));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task ExecutePlatformSpecificFlushAsync(CancellationToken cancellationToken)
    {
        Thread.SpinWait(Random.Shared.Next(Constants.DirectStream.SpinWait.MinDuration,
            Constants.DirectStream.SpinWait.MaxDuration));
        Thread.MemoryBarrier();

        await base.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (_isWindows)
            ExecuteWindowsFlush();
        else
            await ExecuteUnixFlushAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteWindowsFlush()
    {
        if (FlushFileBuffers(SafeFileHandle) is not true)
            throw new IOException("FlushFileBuffers failed" + $" for handle: {SafeFileHandle.DangerousGetHandle()}" +
                                  $" with error: {Marshal.GetLastWin32Error()}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task ExecuteUnixFlushAsync()
    {
        if (fsync(_handles.FileDescriptor) is not 0)
            throw new IOException($"fsync failed for file descriptor: {_handles.FileDescriptor} " +
                                  $"with error: {Marshal.GetLastWin32Error()}");

        await FinalizeUnixFlushAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task FinalizeUnixFlushAsync()
    {
        SecurityService.ProcessPaddingBuffer();
        Thread.MemoryBarrier();

        var result = posix_fadvise(
            _handles.FileDescriptor,
            0,
            Length,
            Constants.Linux.FileAdvice.DontNeed);

        if (result is not 0)
            _logger?.LogWarning(
                "posix_fadvise failed with result: {Result}. File descriptor: {FileDescriptor}, Length: {Length}",
                result,
                _handles.FileDescriptor,
                Length);

        return Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteFlushOperation()
    {
        SecurityService.ProcessPaddingBuffer();
        Interlocked.Exchange(ref _flushState, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        try
        {
            if (disposing) base.Dispose(disposing);

            CloseNativeHandles();
        }
        finally
        {
            _disposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
            CloseNativeHandles();
        }
        finally
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CloseNativeHandles()
    {
        if (_isWindows)
            CloseHandle(_handles.WindowsHandle);
        else
            close(_handles.FileDescriptor);
    }

    ~DirectStream()
    {
        Dispose(true);
    }

    private readonly record struct NativeHandles(int FileDescriptor, IntPtr WindowsHandle);
}