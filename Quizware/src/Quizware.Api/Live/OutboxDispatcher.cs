using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quizware.Infrastructure.Outbox;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Live;

/// <summary>P12-03 (ADR-006): pushes outbox messages to the hubs in the order
/// they happened. It wakes the moment a transaction writing them commits
/// (<see cref="OutboxSignal"/>) and otherwise polls, so nothing waits on
/// memory: a restart picks up whatever is still unprocessed. A failed push
/// leaves the message in place with its error and retry count, holds back
/// that match's later messages (so a screen never sees events out of order)
/// and is retried with a doubling delay; after <see cref="LiveOptions.MaxRetries"/>
/// it is left undelivered so it cannot block the match for good.</summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxSignal _signal;
    private readonly LiveOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly Dictionary<Guid, DateTime> _notBefore = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OutboxDispatcher(IServiceScopeFactory scopes, OutboxSignal signal, IOptions<LiveOptions> options, ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.BackgroundServicesEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
                await _signal.WaitAsync(TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must outlive any single bad batch (e.g. the database briefly unavailable).
                _logger.LogError(ex, "Outbox dispatch failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
            }
        }
    }

    /// <summary>One pass over the pending messages. Returns how many were delivered.
    /// Matches are pushed side by side (up to <see cref="LiveOptions.DispatchConcurrency"/>),
    /// each in its own scope; within a match, strictly in order, stopping at
    /// the first message that fails or is waiting for its retry (P15-06).</summary>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.OutboxMessages
                .Where(m => m.ProcessedAtUtc == null && m.RetryCount < _options.MaxRetries)
                .OrderBy(m => m.OccurredAtUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            var byMatch = new List<List<(OutboxMessage Message, LiveDispatch Dispatch)>>();
            var index = new Dictionary<Guid, int>();
            foreach (var message in pending)
            {
                if (!TryRead(message, out var dispatch))
                {
                    message.RetryCount = _options.MaxRetries;
                    message.Error = "Unreadable payload.";
                    continue;
                }

                if (!index.TryGetValue(dispatch.MatchId, out var slot))
                {
                    index[dispatch.MatchId] = slot = byMatch.Count;
                    byMatch.Add([]);
                }

                byMatch[slot].Add((message, dispatch));
            }

            var now = DateTime.UtcNow;
            using var lanes = new SemaphoreSlim(Math.Max(_options.DispatchConcurrency, 1));
            var outcomes = await Task.WhenAll(byMatch.Select(async group =>
            {
                await lanes.WaitAsync(cancellationToken);
                try
                {
                    return await PushInOrderAsync(group, now, cancellationToken);
                }
                finally
                {
                    lanes.Release();
                }
            }));

            var delivered = 0;
            foreach (var (message, error) in outcomes.SelectMany(o => o))
            {
                if (error is null)
                {
                    message.ProcessedAtUtc = DateTime.UtcNow;
                    _notBefore.Remove(message.Id);
                    delivered++;
                    continue;
                }

                message.RetryCount++;
                message.Error = error.Message.Length > 2000 ? error.Message[..2000] : error.Message;
                _notBefore[message.Id] = now.AddMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, message.RetryCount - 1));
                _logger.LogWarning(error, "Live push of {Type} {MessageId} failed (attempt {Attempt} of {Max}).",
                    message.Type, message.Id, message.RetryCount, _options.MaxRetries);
            }

            await db.SaveChangesAsync(cancellationToken);
            return delivered;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Pushes one match's messages in order with its own scope (a
    /// DbContext is not shared across threads). Stops at the first message
    /// that is waiting for a retry or fails, so later ones never overtake it.</summary>
    private async Task<List<(OutboxMessage Message, Exception? Error)>> PushInOrderAsync(
        List<(OutboxMessage Message, LiveDispatch Dispatch)> group, DateTime now, CancellationToken cancellationToken)
    {
        var results = new List<(OutboxMessage, Exception?)>();
        using var scope = _scopes.CreateScope();
        var pusher = scope.ServiceProvider.GetRequiredService<ILivePusher>();
        foreach (var (message, dispatch) in group)
        {
            if (_notBefore.TryGetValue(message.Id, out var due) && due > now)
            {
                break;
            }

            try
            {
                await pusher.PushAsync(dispatch, cancellationToken);
                results.Add((message, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add((message, ex));
                break;
            }
        }

        return results;
    }

    private static bool TryRead(OutboxMessage message, out LiveDispatch dispatch)
    {
        dispatch = null!;
        try
        {
            using var document = JsonDocument.Parse(message.PayloadJson);
            var root = document.RootElement;
            dispatch = new LiveDispatch(
                message.Id,
                message.Type,
                root.GetProperty("programId").GetGuid(),
                root.GetProperty("matchId").GetGuid(),
                message.OccurredAtUtc,
                root.TryGetProperty("data", out var data) ? data.Clone() : default);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }
}
