using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Domain.Enums;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Live;

/// <summary>P12-05: the server's countdown, pushed to both hubs as a
/// <c>TimerTick</c> for every timed question on screen in a running match —
/// computed once per tick from the stored <c>TimerStartedAtUtc</c>, so every
/// screen receives the same number. Ticks stop one interval after reaching
/// zero. They are not routed through the outbox: a lost tick is replaced by
/// the next one a moment later, and the stored start time stays the truth.</summary>
public sealed class LiveTimerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<MatchHub> _matchHub;
    private readonly IHubContext<DisplayHub> _displayHub;
    private readonly IClock _clock;
    private readonly LiveOptions _options;
    private readonly ILogger<LiveTimerService> _logger;

    public LiveTimerService(
        IServiceScopeFactory scopes, IHubContext<MatchHub> matchHub, IHubContext<DisplayHub> displayHub, IClock clock,
        IOptions<LiveOptions> options, ILogger<LiveTimerService> logger)
    {
        _scopes = scopes;
        _matchHub = matchHub;
        _displayHub = displayHub;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.BackgroundServicesEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.TimerTickIntervalMs));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Timer tick failed; the next tick will try again.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var timed = await (
                from question in db.MatchQuestions
                where question.State == MatchQuestionState.Active && question.TimerStartedAtUtc != null && question.TimeLimitSeconds != null
                join match in db.Matches on question.MatchId equals match.Id
                where match.State == MatchState.InProgress
                select new { question.MatchId, question.Id, Started = question.TimerStartedAtUtc!.Value, Limit = question.TimeLimitSeconds!.Value })
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var grace = TimeSpan.FromMilliseconds(_options.TimerTickIntervalMs);
        foreach (var question in timed)
        {
            var elapsed = now - question.Started;
            if (elapsed > TimeSpan.FromSeconds(question.Limit) + grace)
            {
                continue;
            }

            var tick = new TimerTickMessage(question.MatchId, question.Id, Math.Max(0, question.Limit - elapsed.TotalSeconds), question.Limit, now);
            var group = LiveHubBase.GroupFor(question.MatchId);
            await _matchHub.Clients.Group(group).SendAsync(LiveEvents.TimerTick, tick, cancellationToken);
            await _displayHub.Clients.Group(group).SendAsync(LiveEvents.TimerTick, tick, cancellationToken);
        }
    }
}
