namespace SafeObject.Core.Interfaces;

public interface IDirectStream
{
    Task FlushAsync(CancellationToken cancellationToken);

    ValueTask DisposeAsync();
}