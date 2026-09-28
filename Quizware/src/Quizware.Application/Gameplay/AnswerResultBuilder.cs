using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>The scoreboard and "what now" that follow an answer or a reversal,
/// so the console can move on without another round-trip.</summary>
public sealed class AnswerResultBuilder
{
    private readonly IAppDbContext _db;

    public AnswerResultBuilder(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>Call after SaveChangesAsync, so every total reflects this answer.</summary>
    public async Task<RecordAnswerResultDto> BuildAsync(
        Match match, MatchSegment segment, AnswerRecord answer, int points, Guid scoringRuleId, CancellationToken cancellationToken)
    {
        var scores = await _db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken);
        var ordered = scores.OrderByDescending(s => s.TotalPoints).ToList();
        var ranked = new List<RankedTeamScoreDto>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].TotalPoints == ordered[i - 1].TotalPoints ? ranked[i - 1].Rank : i + 1;
            ranked.Add(new RankedTeamScoreDto(ordered[i].TeamId, ordered[i].TotalPoints, rank));
        }

        var teamScore = scores.SingleOrDefault(s => s.MatchParticipantId == answer.MatchParticipantId)?.TotalPoints ?? 0;
        var next = await NextQuestionAsync(match, segment, cancellationToken);

        var segmentComplete = next is null;
        var pendingSegments = await _db.MatchSegments
            .AnyAsync(s => s.MatchId == match.Id && s.State == MatchSegmentState.Pending, cancellationToken);

        return new RecordAnswerResultDto(
            answer.Id,
            answer.Outcome.ToString(),
            points,
            scoringRuleId,
            teamScore,
            ranked,
            next,
            segmentComplete,
            segmentComplete && !pendingSegments);
    }

    /// <summary>The question still on screen (e.g. passed to another team, or
    /// a buzzer question still open to the others), otherwise the segment's
    /// next reserved one and the team whose turn it will be.</summary>
    private async Task<NextQuestionPreviewDto?> NextQuestionAsync(Match match, MatchSegment segment, CancellationToken cancellationToken)
    {
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var teamNames = await MatchSetup.TeamNamesAsync(_db, participants.Select(p => p.TeamId), cancellationToken);
        string? NameOf(Guid? participantId) =>
            participantId is null ? null : teamNames.GetValueOrDefault(participants.Single(p => p.Id == participantId).TeamId);

        var active = await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken);
        if (active is not null)
        {
            return new NextQuestionPreviewDto(active.Id, active.TargetParticipantId, NameOf(active.TargetParticipantId));
        }

        var reserved = (await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken)).FirstOrDefault();
        if (reserved is null)
        {
            return null;
        }

        var nextParticipant = TurnRotation.NextParticipantOrNull(participants, segment);
        return new NextQuestionPreviewDto(reserved.Id, nextParticipant, NameOf(nextParticipant));
    }
}
