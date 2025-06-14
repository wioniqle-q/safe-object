using System.Buffers;
using System.Runtime.CompilerServices;
using Acl.Fs.Stream.Resources;
using Microsoft.Extensions.Logging;
using static Acl.Fs.Abstractions.Constants.StorageConstants;

namespace Acl.Fs.Stream.Abstractions;

internal abstract class DirectStreamBase<TStream> : System.IO.Stream where TStream : System.IO.Stream
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    internal readonly TStream InnerStream;

    protected readonly ILogger? Logger;
    private volatile bool _disposed;

    private long _logicalLength;
    private long _logicalPosition;

    protected DirectStreamBase(TStream innerStream, ILogger? logger = null)
    {
        InnerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        Logger = logger;

        _logicalLength = innerStream.Length;
        _logicalPosition = innerStream.Position;

        ConfigurePlatformProperties();
    }

    public override bool CanRead => InnerStream.CanRead;
    public override bool CanSeek => InnerStream.CanSeek;
    public override bool CanWrite => InnerStream.CanWrite;
    public override long Length => Interlocked.Read(ref _logicalLength);

    public override long Position
    {
        get => Interlocked.Read(ref _logicalPosition);
        set
        {
            ThrowIfDisposed();

            _semaphore.Wait();
            try
            {
                InnerStream.Position = value;
                Interlocked.Exchange(ref _logicalPosition, value);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await InnerStream.FlushAsync(cancellationToken);
            ExecutePlatformSpecificFlush(cancellationToken);

            SynchronizePosition();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void Flush()
    {
        ThrowIfDisposed();

        _semaphore.Wait();
        try
        {
            InnerStream.Flush();
            ExecutePlatformSpecificFlush(CancellationToken.None);

            SynchronizePosition();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        _semaphore.Wait();
        try
        {
            var bytesRead = InnerStream.Read(buffer.AsSpan(offset, count));
            Interlocked.Add(ref _logicalPosition, bytesRead);
            return bytesRead;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var bytesRead = await InnerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            Interlocked.Add(ref _logicalPosition, bytesRead);
            return bytesRead;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var bytesRead = await InnerStream.ReadAsync(buffer, cancellationToken);
            Interlocked.Add(ref _logicalPosition, bytesRead);
            return bytesRead;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        _semaphore.Wait();
        try
        {
            var currentLogicalPosition = Interlocked.Read(ref _logicalPosition);
            var currentLogicalLength = Interlocked.Read(ref _logicalLength);

            var newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => currentLogicalPosition + offset,
                SeekOrigin.End => currentLogicalLength + offset,
                _ => throw new ArgumentException(ErrorMessages.InvalidSeekOrigin, nameof(origin))
            };

            if (newPosition < 0)
                throw new IOException(ErrorMessages.PositionBeforeStart);

            InnerStream.Position = newPosition;
            Interlocked.Exchange(ref _logicalPosition, newPosition);

            return newPosition;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();

        _semaphore.Wait();
        try
        {
            InnerStream.SetLength(value);
            Interlocked.Exchange(ref _logicalLength, value);
            SynchronizePosition();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        _semaphore.Wait();
        try
        {
            var span = buffer.AsSpan(offset, count);
            WriteChunks(span);

            Interlocked.Add(ref _logicalPosition, span.Length);

            var newLength = Math.Max(Interlocked.Read(ref _logicalLength), Interlocked.Read(ref _logicalPosition));
            Interlocked.Exchange(ref _logicalLength, newLength);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var memory = buffer.AsMemory(offset, count);
            await WriteChunksAsync(memory, cancellationToken);

            Interlocked.Add(ref _logicalPosition, memory.Length);

            var newLength = Math.Max(Interlocked.Read(ref _logicalLength), Interlocked.Read(ref _logicalPosition));
            Interlocked.Exchange(ref _logicalLength, newLength);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await WriteChunksAsync(buffer, cancellationToken);

            Interlocked.Add(ref _logicalPosition, buffer.Length);

            var newLength = Math.Max(Interlocked.Read(ref _logicalLength), Interlocked.Read(ref _logicalPosition));
            Interlocked.Exchange(ref _logicalLength, newLength);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void WriteChunks(ReadOnlySpan<byte> buffer)
    {
        var remaining = buffer.Length;
        var offset = 0;

        byte[]? rentedBuffer = null;
        var alignedBuffer = remaining <= SectorSize
            ? stackalloc byte[SectorSize]
            : rentedBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);

        try
        {
            while (remaining > 0)
            {
                var bytesToWrite = Math.Min(SectorSize, remaining);
                var chunk = buffer.Slice(offset, bytesToWrite);

                if (bytesToWrite == SectorSize)
                {
                    InnerStream.Write(chunk);
                }
                else
                {
                    var alignedSize = (bytesToWrite + SectorSize - 1) & ~(SectorSize - 1);

                    alignedBuffer.Clear();
                    chunk.CopyTo(alignedBuffer);

                    InnerStream.Write(alignedBuffer[..alignedSize]);
                }

                offset += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer, true);
        }
    }

    private async Task WriteChunksAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var remaining = buffer.Length;
        var offset = 0;

        var rentedBuffer = ArrayPool<byte>.Shared.Rent(SectorSize);

        try
        {
            while (remaining > 0)
            {
                var bytesToWrite = Math.Min(SectorSize, remaining);
                var chunk = buffer.Slice(offset, bytesToWrite);

                if (bytesToWrite is SectorSize)
                {
                    await InnerStream.WriteAsync(chunk, cancellationToken);
                }
                else
                {
                    var alignedSize = (bytesToWrite + SectorSize - 1) & ~(SectorSize - 1);

                    Array.Clear(rentedBuffer, 0, alignedSize);
                    chunk.Span.CopyTo(rentedBuffer);

                    await InnerStream.WriteAsync(rentedBuffer.AsMemory(0, alignedSize), cancellationToken);
                }

                offset += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer, true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false)) return;

        if (disposing)
        {
            InnerStream.Dispose();
            _semaphore.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false)) return;

        if (InnerStream is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            InnerStream.Dispose();

        _semaphore.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ConfigurePlatformProperties()
    {
        ConfigurePlatformPropertiesCore();
    }

    private void SynchronizePosition()
    {
        var actualPosition = InnerStream.Position;
        Interlocked.Exchange(ref _logicalPosition, actualPosition);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    protected abstract void ConfigurePlatformPropertiesCore();
    protected abstract void ExecutePlatformSpecificFlush(CancellationToken cancellationToken);
}