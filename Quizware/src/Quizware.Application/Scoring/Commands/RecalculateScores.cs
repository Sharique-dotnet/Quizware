using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Application.Scoring.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Scoring.Commands;

public sealed record RecalculateScoresCommand(Guid MatchId) : IRequest<IReadOnlyList<TeamScoreItemDto>>;

/// <summary>P10-06: the safety net. Rebuilds the match's TeamMatchScore rows
/// from the ScoreEvent ledger, then the stage's TeamStageScore rows from the
/// match totals. With a healthy ledger the result equals the incremental
/// totals exactly; if they had drifted, the ledger wins.</summary>
public sealed class RecalculateScoresCommandHandler : IRequestHandler<RecalculateScoresCommand, IReadOnlyList<TeamScoreItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly IScoringEngine _scoring;
    private readonly MatchCompletion _completion;
    private readonly MatchEventLog _eventLog;

    public RecalculateScoresCommandHandler(IAppDbContext db, IScoringEngine scoring, MatchCompletion completion, MatchEventLog eventLog)
    {
        _db = db;
        _scoring = scoring;
        _completion = completion;
        _eventLog = eventLog;
    }

    public async Task<IReadOnlyList<TeamScoreItemDto>> Handle(RecalculateScoresCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchScoreboard.LoadMatchAsync(_db, request.MatchId, cancellationToken);
        if (match.State is MatchState.Draft or MatchState.Ready)
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} has not started; there are no scores to recalculate.");
        }

        var rebuilt = await _scoring.RecalculateMatchAsync(match, cancellationToken);
        if (match.State == MatchState.Completed)
        {
            await _completion.ReviseResultAsync(match, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _scoring.RecalculateStageAsync(match.StageId, cancellationToken);
        await _eventLog.AppendAsync(match, MatchEventTypes.ScoresRecalculated, new
        {
            totals = rebuilt.Select(s => new { teamId = s.TeamId, points = s.TotalPoints }),
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await MatchScoreboard.BuildAsync(_db, match, cancellationToken);
    }
}
