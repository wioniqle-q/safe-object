using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace SafeObject.Core.Services.Base;

public abstract class DirectStreamBase : FileStream
{
    protected readonly ILogger? Logger;

    private volatile bool _disposed;
    private volatile int _flushState;

    protected DirectStreamBase(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        ILogger? logger)
        : base(path ?? throw new ArgumentNullException(nameof(path)),
            mode, access, share, bufferSize, options | FileOptions.WriteThrough)
    {
        Logger = logger;
        ConfigurePlatformProperties();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _flushState, 1) is 1)
            return;

        try
        {
            await base.FlushAsync(cancellationToken);
            await ExecutePlatformSpecificFlushAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _flushState, 0);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        try
        {
            if (disposing)
                base.Dispose(disposing);
        }
        finally
        {
            _disposed = true;
        }
    }

    ~DirectStreamBase()
    {
        Dispose(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigurePlatformProperties()
    {
        ConfigurePlatformPropertiesCore();
    }

    protected abstract void ConfigurePlatformPropertiesCore();
    protected abstract Task ExecutePlatformSpecificFlushAsync(CancellationToken _);
}