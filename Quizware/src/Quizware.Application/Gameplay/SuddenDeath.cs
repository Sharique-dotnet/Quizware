using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>P9-15 / BR-5.6: a sudden-death segment closes as soon as one team
/// leads, provided every active team has faced the same number of questions
/// (the segment's served count is a whole number of rounds). Unplayed
/// questions go back to the pool. Call after the question's outcome is
/// applied and before SaveChangesAsync.</summary>
public sealed class SuddenDeath
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;

    public SuddenDeath(IAppDbContext db, MatchEventLog eventLog)
    {
        _db = db;
        _eventLog = eventLog;
    }

    /// <summary>Returns true when it closed the segment.</summary>
    public async Task<bool> TryCloseAsync(Match match, MatchSegment segment, CancellationToken cancellationToken)
    {
        if (!segment.IsSuddenDeath || segment.State != MatchSegmentState.Open)
        {
            return false;
        }

        if (await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken) is { State: MatchQuestionState.Active })
        {
            return false;
        }

        var active = (await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken))
            .Where(p => p.Status == ParticipantStatus.Active)
            .Select(p => p.Id)
            .ToList();
        if (active.Count == 0 || segment.ServedQuestionCount == 0 || segment.ServedQuestionCount % active.Count != 0)
        {
            return false;
        }

        var totals = await _db.TeamMatchScores
            .Where(s => s.MatchId == match.Id && active.Contains(s.MatchParticipantId))
            .Select(s => new { s.MatchParticipantId, s.TotalPoints })
            .ToListAsync(cancellationToken);
        var local = _db.TeamMatchScores.Local.Where(s => s.MatchId == match.Id).ToDictionary(s => s.MatchParticipantId, s => s.TotalPoints);
        var points = totals.Select(t => local.GetValueOrDefault(t.MatchParticipantId, t.TotalPoints)).OrderByDescending(p => p).ToList();
        if (points.Count < 2 || points[0] == points[1])
        {
            return false;
        }

        segment.Complete();
        var unplayed = await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken);
        foreach (var question in unplayed)
        {
            question.Release();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.SegmentCompleted, new
        {
            segmentId = segment.Id,
            served = segment.ServedQuestionCount,
            released = unplayed.Count,
            reason = "Sudden death: one team leads",
        }, cancellationToken);
        return true;
    }
}
