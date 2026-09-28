using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Selection;
using Quizware.Domain.Enums;
using Quizware.Application.Scoring;

namespace Quizware.Application.Gameplay.Commands;

public sealed record StartMatchCommand(Guid MatchId) : IRequest<StartMatchResultDto>;

/// <summary>One unit of work: the match starts, every segment's questions are
/// drawn and reserved, and a score row is opened per participant — or, if any
/// segment's pool runs dry (QUESTION_POOL_EXHAUSTED), nothing is saved at all.
/// Reserving everything up front means a crash mid-match never changes what
/// comes next.</summary>
public sealed class StartMatchCommandHandler : IRequestHandler<StartMatchCommand, StartMatchResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IQuestionSelector _selector;
    private readonly MatchEventLog _eventLog;
    private readonly IScoringEngine _scoring;
    private readonly IMatchNotifications _notifications;

    public StartMatchCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, IQuestionSelector selector, MatchEventLog eventLog, IScoringEngine scoring,
        IMatchNotifications notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _selector = selector;
        _eventLog = eventLog;
        _scoring = scoring;
        _notifications = notifications;
    }

    public async Task<StartMatchResultDto> Handle(StartMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var active = participants.Where(p => p.Status == ParticipantStatus.Active).ToList();

        match.Start(active.Count);

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var reserved = 0;
        foreach (var segment in segments)
        {
            // A distinct seed per segment, derived from the match's stored
            // seed, so two segments of one format do not mirror each other's
            // draw while the whole match stays reproducible.
            var seed = unchecked(match.RandomSeed + segment.OrderIndex + 1);
            var result = await _selector.SelectAndReserveAsync(
                new SelectionRequest(match.ProgramId, match.StageId, segment.SegmentTemplateId, segment.FormatCode,
                    segment.PlannedQuestionCount, seed, match.Id),
                segment.Id,
                actor,
                cancellationToken);
            reserved += result.Questions.Count;
        }

        await _scoring.OpenScoresAsync(match, participants, cancellationToken);

        await _eventLog.AppendAsync(match, MatchEventTypes.MatchStarted, new
        {
            randomSeed = match.RandomSeed,
            participantIds = active.Select(p => p.Id),
            segmentIds = segments.Select(s => s.Id),
            questionsReserved = reserved,
        }, cancellationToken);
        _notifications.Publish("MatchStateChanged", match.ProgramId, match.Id, new { state = match.State.ToString(), questionsReserved = reserved });

        await _db.SaveChangesAsync(cancellationToken);

        var teams = await _db.Teams
            .Where(t => participants.Select(p => p.TeamId).Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        return new StartMatchResultDto(
            match.Id,
            match.State.ToString(),
            match.RandomSeed,
            match.StartedAtUtc!.Value,
            participants
                .Select(p => new LiveParticipantDto(
                    p.Id, p.TeamId, teams.GetValueOrDefault(p.TeamId)?.DisplayName ?? string.Empty, p.SeatNumber,
                    p.TurnOrder, p.Status.ToString(), teams.GetValueOrDefault(p.TeamId)?.ScoreImageUrl, p.BuzzDeviceId))
                .ToList(),
            segments
                .Select(s => new LiveSegmentDto(s.Id, s.FormatCode.ToString(), s.OrderIndex, s.PlannedQuestionCount, s.State.ToString()))
                .ToList(),
            reserved,
            LiveStateBuilder.BuzzerStatus.Available);
    }
}
