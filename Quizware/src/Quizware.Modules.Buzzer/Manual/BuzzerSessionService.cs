using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quizware.Application.Abstractions;
using Quizware.Application.Buzzer;
using Quizware.Application.Common.Exceptions;
using Quizware.Application.Gameplay;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Buzzer;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Manual;

/// <summary>P14-03: persists buzz sessions and every press (with the raw
/// device frame, as dispute evidence), maps devices to participants, and
/// ranks presses. It only ever says who pressed first: it never records an
/// answer and never touches scores — the operator does that through the
/// normal answer endpoint, and can always override the buzzer.</summary>
public sealed class BuzzerSessionService : IBuzzerSessionService
{
    private readonly IAppDbContext _db;
    private readonly IBuzzerProvider _provider;
    private readonly IMatchNotifications _notifications;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly BuzzerOptions _options;
    private readonly ILogger<BuzzerSessionService> _logger;

    public BuzzerSessionService(
        IAppDbContext db,
        IBuzzerProvider provider,
        IMatchNotifications notifications,
        IClock clock,
        ICurrentUser currentUser,
        IOptions<BuzzerOptions> options,
        ILogger<BuzzerSessionService> logger)
    {
        _db = db;
        _provider = provider;
        _notifications = notifications;
        _clock = clock;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The module's tables are part of the same AppDbContext (its
    /// entity configurations are contributed through
    /// <see cref="IEntityConfigurationAssemblyMarker"/>), but IAppDbContext
    /// only exposes the core sets — so the module reaches its own sets
    /// through the DbContext behind the port, in the same unit of work.</summary>
    private DbContext Context => _db as DbContext
        ?? throw new InvalidOperationException("The buzzer module needs IAppDbContext to be an EF Core DbContext.");

    private DbSet<BuzzSession> Sessions => Context.Set<BuzzSession>();

    private DbSet<BuzzPress> Presses => Context.Set<BuzzPress>();

    private DbSet<BuzzDeviceMapping> Mappings => Context.Set<BuzzDeviceMapping>();

    private string Actor => _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "buzzer-agent";

    /// <summary>Replaces the match's device mapping. Rows are updated in place
    /// per device, so the unique (MatchId, DeviceId) index never sees two
    /// active rows at once (D-023).</summary>
    public async Task SetMappingsAsync(Guid matchId, IReadOnlyList<DeviceMappingInput> mappings, CancellationToken cancellationToken)
    {
        var match = await LoadMatchAsync(matchId, cancellationToken);
        var participants = await _db.MatchParticipants.Where(p => p.MatchId == match.Id).ToListAsync(cancellationToken);
        var unknown = mappings.Where(m => participants.All(p => p.Id != m.MatchParticipantId)).Select(m => m.MatchParticipantId).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["Mappings"] = [$"Not participants of this match: {string.Join(", ", unknown)}."],
            });
        }

        var now = _clock.UtcNow;
        var existing = await Mappings.Where(m => m.MatchId == match.Id && !m.IsDeleted).ToListAsync(cancellationToken);
        foreach (var row in existing.Where(r => mappings.All(m => m.DeviceId != r.DeviceId)))
        {
            row.IsDeleted = true;
            row.IsActive = false;
            row.DeletedAtUtc = now;
            row.UpdatedAtUtc = now;
            row.UpdatedBy = Actor;
        }

        foreach (var mapping in mappings)
        {
            var participant = participants.Single(p => p.Id == mapping.MatchParticipantId);
            var row = existing.FirstOrDefault(r => r.DeviceId == mapping.DeviceId);
            if (row is null)
            {
                row = new BuzzDeviceMapping
                {
                    Id = Guid.NewGuid(),
                    ProgramId = match.ProgramId,
                    MatchId = match.Id,
                    DeviceId = mapping.DeviceId,
                    CreatedAtUtc = now,
                    CreatedBy = Actor,
                };
                Mappings.Add(row);
            }
            else
            {
                row.UpdatedAtUtc = now;
                row.UpdatedBy = Actor;
            }

            row.MatchParticipantId = participant.Id;
            row.TeamId = participant.TeamId;
            row.SeatNumber = participant.SeatNumber;
            row.IsActive = true;
        }

        foreach (var participant in participants)
        {
            participant.AssignBuzzDevice(mappings.FirstOrDefault(m => m.MatchParticipantId == participant.Id)?.DeviceId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Arms a session for the live question. Any session still armed
    /// for the match is superseded. If the hardware cannot be armed, the
    /// session is kept as Failed (with the reason) and the call is a 503 —
    /// the operator carries on with the manual buttons.</summary>
    public async Task<BuzzSessionDto> ArmAsync(Guid matchId, Guid segmentId, Guid matchQuestionId, CancellationToken cancellationToken)
    {
        if (!_provider.IsAvailable)
        {
            throw new BuzzerUnavailableException($"No buzzer is available (provider: {_provider.Name}). Record the answer manually.");
        }

        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match.State != MatchState.InProgress)
        {
            throw new InvalidStateTransitionException($"A buzz session can only be armed while the match is in progress; it is {match.State}.");
        }

        if (!await _db.MatchSegments.AnyAsync(s => s.Id == segmentId && s.MatchId == match.Id, cancellationToken))
        {
            throw new KeyNotFoundException($"Segment '{segmentId}' was not found in this match.");
        }

        var question = await _db.MatchQuestions
            .SingleOrDefaultAsync(q => q.Id == matchQuestionId && q.MatchId == match.Id && q.MatchSegmentId == segmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{matchQuestionId}' was not found in this segment.");
        if (question.State != MatchQuestionState.Active)
        {
            throw new InvalidStateTransitionException("A buzz session can only be armed for the question that is live.");
        }

        var now = _clock.UtcNow;
        foreach (var stale in await Sessions.Where(s => s.MatchId == match.Id && s.State == BuzzSessionState.Armed).ToListAsync(cancellationToken))
        {
            stale.State = BuzzSessionState.Reset;
            stale.FailureReason = "Superseded by a newer session.";
            stale.UpdatedAtUtc = now;
            stale.UpdatedBy = Actor;
        }

        var session = new BuzzSession
        {
            Id = Guid.NewGuid(),
            ProgramId = match.ProgramId,
            MatchId = match.Id,
            MatchQuestionId = question.Id,
            State = BuzzSessionState.Armed,
            ArmedAtUtc = now,
            Provider = _provider.Name,
            WindowMs = _options.WindowMs,
            CreatedAtUtc = now,
            CreatedBy = Actor,
        };
        Sessions.Add(session);

        var devices = await DeviceIdsAsync(match.Id, cancellationToken);
        try
        {
            await BuzzerCalls.GuardAsync(() => _provider.ArmAsync(new BuzzArmRequest(session.Id, match.Id, devices, session.WindowMs), cancellationToken));
        }
        catch (BuzzerUnavailableException ex)
        {
            session.State = BuzzSessionState.Failed;
            session.FailureReason = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Arming buzz session {SessionId} for match {MatchId} failed", session.Id, match.Id);
            throw;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(session, cancellationToken);
    }

    public async Task<BuzzSessionDto> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await ToDtoAsync(await LoadSessionAsync(sessionId, cancellationToken), cancellationToken);

    /// <summary>Records a pushed press. The first press per device counts;
    /// a repeat (the agent retrying, say) changes nothing.</summary>
    public async Task<BuzzSessionDto> SubmitPressAsync(
        Guid sessionId, int deviceId, long elapsedMs, string? rawFrameHex, string? buttonKey, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session.State != BuzzSessionState.Armed)
        {
            throw new InvalidStateTransitionException($"The buzz session is {session.State}; presses are accepted only while it is armed.");
        }

        if (await Presses.AnyAsync(p => p.BuzzSessionId == session.Id && p.DeviceId == deviceId, cancellationToken))
        {
            return await ToDtoAsync(session, cancellationToken);
        }

        await AddPressesAsync(session, [new BuzzPressResult(deviceId, buttonKey ?? string.Empty, (int)elapsedMs, rawFrameHex?.ToUpperInvariant())], cancellationToken);
        var dto = await ToDtoAsync(session, cancellationToken);
        Announce(session, dto);
        await _db.SaveChangesAsync(cancellationToken);
        return dto;
    }

    /// <summary>Reads whatever the provider has now (a pull provider polls the
    /// devices; the HTTP agent has already pushed), closes the session and
    /// announces the ranking. A provider failure marks the session Failed and
    /// is a 503; collecting again is harmless.</summary>
    public async Task<BuzzSessionDto> CollectAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session.State == BuzzSessionState.Collected)
        {
            return await ToDtoAsync(session, cancellationToken);
        }

        if (session.State != BuzzSessionState.Armed)
        {
            throw new InvalidStateTransitionException($"The buzz session is {session.State}; only an armed session can be collected.");
        }

        IReadOnlyList<BuzzPressResult> fresh;
        try
        {
            var devices = await DeviceIdsAsync(session.MatchId, cancellationToken);
            fresh = await BuzzerCalls.GuardAsync(() => _provider.CollectAsync(session.Id, devices, cancellationToken));
        }
        catch (BuzzerUnavailableException ex)
        {
            session.State = BuzzSessionState.Failed;
            session.FailureReason = ex.Message;
            session.UpdatedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Collecting buzz session {SessionId} failed", session.Id);
            throw;
        }

        var already = await Presses.Where(p => p.BuzzSessionId == session.Id).Select(p => p.DeviceId).ToListAsync(cancellationToken);
        await AddPressesAsync(session, fresh.Where(p => !already.Contains(p.DeviceId)).GroupBy(p => p.DeviceId).Select(g => g.First()).ToList(), cancellationToken);
        session.State = BuzzSessionState.Collected;
        session.CollectedAtUtc = _clock.UtcNow;
        session.UpdatedAtUtc = session.CollectedAtUtc;
        session.UpdatedBy = Actor;

        var dto = await ToDtoAsync(session, cancellationToken);
        Announce(session, dto);
        await _db.SaveChangesAsync(cancellationToken);
        return dto;
    }

    /// <summary>Resets the devices (best effort — a buzzer that cannot be
    /// reached is logged, not an error) and closes the session.</summary>
    public async Task ResetAsync(Guid sessionId, string? reason, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        try
        {
            var devices = await DeviceIdsAsync(session.MatchId, cancellationToken);
            await BuzzerCalls.GuardAsync(() => _provider.ResetAsync(session.Id, devices, cancellationToken));
        }
        catch (BuzzerUnavailableException ex)
        {
            _logger.LogWarning(ex, "Resetting the devices for buzz session {SessionId} failed", session.Id);
        }

        session.State = BuzzSessionState.Reset;
        session.FailureReason = string.IsNullOrWhiteSpace(reason) ? session.FailureReason : $"Reset: {reason.Trim()}";
        session.UpdatedAtUtc = _clock.UtcNow;
        session.UpdatedBy = Actor;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LiveBuzzerStatusDto> LiveStatusAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var latest = await Sessions
            .Where(s => s.MatchId == matchId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var available = _provider.IsAvailable;
        return new LiveBuzzerStatusDto(available, latest?.Id, latest?.State.ToString() ?? (available ? "Idle" : "Unavailable"));
    }

    public async Task<IReadOnlyList<ArmedBuzzSessionDto>> ArmedSessionsAsync(CancellationToken cancellationToken)
    {
        var armed = await Sessions.Where(s => s.State == BuzzSessionState.Armed && !s.IsDeleted).OrderBy(s => s.ArmedAtUtc).ToListAsync(cancellationToken);
        var result = new List<ArmedBuzzSessionDto>(armed.Count);
        foreach (var session in armed)
        {
            result.Add(new ArmedBuzzSessionDto(
                session.Id, session.MatchId, await DeviceIdsAsync(session.MatchId, cancellationToken), session.WindowMs, session.ArmedAtUtc ?? session.CreatedAtUtc));
        }

        return result;
    }

    private async Task AddPressesAsync(BuzzSession session, IReadOnlyList<BuzzPressResult> presses, CancellationToken cancellationToken)
    {
        if (presses.Count == 0)
        {
            return;
        }

        var teams = await Mappings
            .Where(m => m.MatchId == session.MatchId && !m.IsDeleted && m.IsActive)
            .ToDictionaryAsync(m => m.DeviceId, m => m.TeamId, cancellationToken);
        foreach (var press in presses)
        {
            Presses.Add(new BuzzPress
            {
                Id = Guid.NewGuid(),
                ProgramId = session.ProgramId,
                BuzzSessionId = session.Id,
                DeviceId = press.DeviceId,
                TeamId = teams.GetValueOrDefault(press.DeviceId),
                ButtonKey = press.ButtonKey,
                ElapsedMs = press.ElapsedMs,
                RawFrameHex = press.RawFrameHex,
                ReceivedAtUtc = _clock.UtcNow,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        var all = await Presses.Where(p => p.BuzzSessionId == session.Id).OrderBy(p => p.ReceivedAtUtc).ToListAsync(cancellationToken);
        foreach (var (press, rank) in BuzzRankingCalculator.Rank(all, p => p.ElapsedMs))
        {
            press.Rank = rank;
        }
    }

    private void Announce(BuzzSession session, BuzzSessionDto dto) =>
        _notifications.Publish(LiveEvents.BuzzResultsReady, session.ProgramId, session.MatchId, new
        {
            sessionId = session.Id,
            matchQuestionId = session.MatchQuestionId,
            state = dto.State,
            presses = dto.RankedPresses.Select(p => new { p.DeviceId, p.MatchParticipantId, p.TeamId, p.TeamName, p.ElapsedMs, p.Rank }).ToList(),
        });

    private async Task<IReadOnlyList<int>> DeviceIdsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var mapped = await Mappings
            .Where(m => m.MatchId == matchId && !m.IsDeleted && m.IsActive)
            .OrderBy(m => m.DeviceId)
            .Select(m => m.DeviceId)
            .ToListAsync(cancellationToken);
        return mapped.Count > 0 ? mapped : Enumerable.Range(1, Math.Max(_provider.DeviceCount, 1)).ToList();
    }

    private async Task<Match> LoadMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        await _db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, cancellationToken)
        ?? throw new KeyNotFoundException($"Match '{matchId}' was not found.");

    /// <summary>The session's match goes through the tenant filter, so an
    /// operator never sees another program's session. The agent has no
    /// program in its credential, so it can reach any session it was told about.</summary>
    private async Task<BuzzSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await Sessions.SingleOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, cancellationToken);
        if (session is null || !await _db.Matches.AnyAsync(m => m.Id == session.MatchId, cancellationToken))
        {
            throw new KeyNotFoundException($"Buzz session '{sessionId}' was not found.");
        }

        return session;
    }

    private async Task<BuzzSessionDto> ToDtoAsync(BuzzSession session, CancellationToken cancellationToken)
    {
        // Tracked rows come back with any rank set since the last save.
        var presses = (await Presses.Where(p => p.BuzzSessionId == session.Id).ToListAsync(cancellationToken))
            .OrderBy(p => p.Rank == 0 ? int.MaxValue : p.Rank)
            .ThenBy(p => p.ReceivedAtUtc)
            .ToList();

        var mappings = await Mappings
            .Where(m => m.MatchId == session.MatchId && !m.IsDeleted && m.IsActive)
            .ToDictionaryAsync(m => m.DeviceId, cancellationToken);
        var participants = await _db.MatchParticipants
            .Where(p => p.MatchId == session.MatchId)
            .Select(p => new { p.Id, p.TeamId })
            .ToListAsync(cancellationToken);
        var teamIds = presses.Where(p => p.TeamId != null).Select(p => p.TeamId!.Value).Distinct().ToList();
        var names = await _db.Teams.IgnoreQueryFilters()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);

        return new BuzzSessionDto(
            session.Id,
            session.MatchId,
            session.MatchQuestionId,
            session.State.ToString(),
            session.Provider,
            session.WindowMs,
            session.FailureReason,
            session.ArmedAtUtc,
            session.CollectedAtUtc,
            presses
                .Select(p => new BuzzPressItemDto(
                    p.Id,
                    p.DeviceId,
                    p.TeamId is { } teamId ? participants.FirstOrDefault(x => x.TeamId == teamId)?.Id : mappings.GetValueOrDefault(p.DeviceId)?.MatchParticipantId,
                    p.TeamId,
                    p.TeamId is { } id ? names.GetValueOrDefault(id) : null,
                    p.ButtonKey,
                    p.ElapsedMs,
                    p.Rank,
                    p.RawFrameHex,
                    p.ReceivedAtUtc))
                .ToList());
    }
}
