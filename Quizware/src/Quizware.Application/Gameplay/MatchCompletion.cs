using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Scoring;
using Quizware.Application.Selection;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Completing a match — by the operator, or automatically when too
/// few teams remain. Ranks use standard competition ranking (1, 1, 3); a tie
/// for first leaves the match without a winner and IsTied set, which is what
/// the tie-break flow keys off. A participant excluded from standings keeps
/// its score but is not ranked.</summary>
public sealed class MatchCompletion
{
    private readonly IAppDbContext _db;
    private readonly IQuestionSelector _selector;
    private readonly MatchEventLog _eventLog;
    private readonly IScoringEngine _scoring;

    public MatchCompletion(IAppDbContext db, IQuestionSelector selector, MatchEventLog eventLog, IScoringEngine scoring)
    {
        _db = db;
        _selector = selector;
        _eventLog = eventLog;
        _scoring = scoring;
    }

    /// <summary>Does not call SaveChangesAsync — the caller owns the unit of work.</summary>
    public async Task CompleteAsync(Match match, string reason, CancellationToken cancellationToken)
    {
        if (match.State != MatchState.InProgress)
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} must be InProgress to complete; it is {match.State}.");
        }

        await CloseOutstandingWorkAsync(match, "Match ended", cancellationToken);

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var result = await RankAsync(match, participants, cancellationToken);

        match.Complete(result.WinnerTeamId, result.IsTied);
        await _scoring.RecordMatchCompletedAsync(match, participants, cancellationToken);
        await _eventLog.AppendAsync(match, MatchEventTypes.MatchCompleted, new
        {
            reason,
            winnerTeamId = result.WinnerTeamId,
            isTied = result.IsTied,
            results = result.Ranked.Select(p => new { participantId = p.Id, points = p.FinalScore, rank = p.FinalRank }),
        }, cancellationToken);
    }

    /// <summary>Re-ranks an already completed match after its scores changed
    /// (a manual adjustment or a recalculation), updating the winner too.</summary>
    public async Task ReviseResultAsync(Match match, CancellationToken cancellationToken)
    {
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var result = await RankAsync(match, participants, cancellationToken);
        match.ReviseResult(result.WinnerTeamId, result.IsTied);
    }

    private sealed record Ranking(IReadOnlyList<MatchParticipant> Ranked, Guid? WinnerTeamId, bool IsTied);

    private async Task<Ranking> RankAsync(Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken)
    {
        var scores = await _db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken);
        var scoreByParticipant = scores.ToDictionary(s => s.MatchParticipantId);

        var ranked = participants
            .Where(p => !p.ExcludeFromStandings)
            .Select(p => (Participant: p, Points: scoreByParticipant.GetValueOrDefault(p.Id)?.TotalPoints ?? 0))
            .OrderByDescending(x => x.Points)
            .ToList();

        foreach (var participant in participants.Where(p => p.ExcludeFromStandings))
        {
            participant.RecordResult(scoreByParticipant.GetValueOrDefault(participant.Id)?.TotalPoints ?? 0, null);
        }

        for (var i = 0; i < ranked.Count; i++)
        {
            var rank = i > 0 && ranked[i].Points == ranked[i - 1].Points
                ? ranked[i - 1].Participant.FinalRank!.Value
                : i + 1;
            ranked[i].Participant.RecordResult(ranked[i].Points, rank);
            scoreByParticipant.GetValueOrDefault(ranked[i].Participant.Id)?.SetRank(rank);
        }

        var isTied = ranked.Count > 1 && ranked[0].Points == ranked[1].Points;
        var winnerTeamId = ranked.Count == 0 || isTied ? (Guid?)null : ranked[0].Participant.TeamId;
        return new Ranking(ranked.Select(r => r.Participant).ToList(), winnerTeamId, isTied);
    }

    /// <summary>Whatever was left mid-flight: the active question is skipped,
    /// the open segment completed, pending segments skipped, and every question
    /// still only reserved goes back to the pool for other matches.</summary>
    public async Task CloseOutstandingWorkAsync(Match match, string reason, CancellationToken cancellationToken)
    {
        var activeQuestion = await _db.MatchQuestions
            .SingleOrDefaultAsync(q => q.MatchId == match.Id && q.State == MatchQuestionState.Active, cancellationToken);
        // The query matches on the stored state; the tracked instance may
        // already have been closed earlier in this same unit of work.
        if (activeQuestion is { State: MatchQuestionState.Active })
        {
            activeQuestion.Skip();
        }

        foreach (var segment in await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken))
        {
            if (segment.State == MatchSegmentState.Open)
            {
                segment.Complete();
            }
            else if (segment.State == MatchSegmentState.Pending)
            {
                segment.Skip(reason);
            }
        }

        await _selector.ReleaseReservationsAsync(match.Id, cancellationToken);
    }
}
