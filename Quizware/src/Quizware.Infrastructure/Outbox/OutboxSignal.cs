namespace Quizware.Infrastructure.Outbox;

/// <summary>Wakes the outbox dispatcher as soon as a transaction that wrote
/// outbox messages commits, so a live push does not wait for the next poll.
/// Signals that arrive while the dispatcher is busy collapse into one wake-up.</summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled and not yet consumed — one wake-up covers both.
        }
    }

    /// <summary>True when signalled; false when <paramref name="timeout"/> passed first.</summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);
}
