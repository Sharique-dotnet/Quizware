using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Scoring.Dtos;
using Quizware.Domain.Enums;

namespace Quizware.Application.Scoring.Queries;

public sealed record GetScoreEventsQuery(Guid MatchId) : IRequest<IReadOnlyList<ScoreEventItemDto>>;

/// <summary>The match's score ledger in the order it was written. Events
/// without a written reason (ordinary answers) are labelled by the outcome
/// that scored them.</summary>
public sealed class GetScoreEventsQueryHandler : IRequestHandler<GetScoreEventsQuery, IReadOnlyList<ScoreEventItemDto>>
{
    private readonly IAppDbContext _db;

    public GetScoreEventsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ScoreEventItemDto>> Handle(GetScoreEventsQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchScoreboard.LoadMatchAsync(_db, request.MatchId, cancellationToken);
        var events = await _db.ScoreEvents
            .Where(e => e.MatchId == match.Id)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        var answerIds = events.Where(e => e.AnswerRecordId != null).Select(e => e.AnswerRecordId!.Value).ToList();
        var outcomes = await _db.AnswerRecords
            .Where(a => answerIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Outcome, cancellationToken);

        return events
            .Select(e => new ScoreEventItemDto(
                e.Id,
                e.TeamId,
                e.Points,
                e.Reason ?? Describe(e.EventType, e.AnswerRecordId is { } id ? outcomes.GetValueOrDefault(id) : null),
                e.IsReversed,
                e.OccurredAtUtc))
            .ToList();
    }

    private static string Describe(ScoreEventType type, AnswerOutcome? outcome) =>
        type == ScoreEventType.Answer && outcome is not null ? $"Answer: {outcome}" : type.ToString();
}
