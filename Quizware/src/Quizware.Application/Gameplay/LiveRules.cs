using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

internal static class LiveRules
{
    /// <summary>Play actions need a running match — a paused one must be
    /// resumed first, so nothing happens on stage while the clock is stopped.</summary>
    public static void RequireInProgress(Match match)
    {
        if (match.State != MatchState.InProgress)
        {
            throw new InvalidStateTransitionException(
                $"Match {match.MatchNumber} is {match.State}; it must be in progress for this action.");
        }
    }

    /// <summary>Which segments may be opened next. Nothing, while one is
    /// already open. Under OperatorChoice any pending segment; otherwise only
    /// the next pending one in order.</summary>
    public static IReadOnlyList<MatchSegment> OpenableSegments(Stage stage, IReadOnlyList<MatchSegment> segments)
    {
        if (segments.Any(s => s.State == MatchSegmentState.Open))
        {
            return [];
        }

        var pending = segments.Where(s => s.State == MatchSegmentState.Pending).OrderBy(s => s.OrderIndex).ToList();
        return stage.SegmentOrderMode == SegmentOrderMode.OperatorChoice ? pending : pending.Take(1).ToList();
    }

    public static async Task<MatchSegment> LoadSegmentAsync(
        IAppDbContext db, Guid matchId, Guid segmentId, CancellationToken cancellationToken) =>
        await db.MatchSegments.SingleOrDefaultAsync(s => s.Id == segmentId && s.MatchId == matchId, cancellationToken)
        ?? throw new KeyNotFoundException($"Segment '{segmentId}' was not found in this match.");

    public static async Task<MatchQuestion> LoadQuestionAsync(
        IAppDbContext db, Guid matchId, Guid matchQuestionId, CancellationToken cancellationToken) =>
        await db.MatchQuestions.SingleOrDefaultAsync(q => q.Id == matchQuestionId && q.MatchId == matchId, cancellationToken)
        ?? throw new KeyNotFoundException($"Question '{matchQuestionId}' was not found in this match.");

    public static Task<MatchQuestion?> ActiveQuestionAsync(IAppDbContext db, Guid matchId, CancellationToken cancellationToken) =>
        db.MatchQuestions.SingleOrDefaultAsync(q => q.MatchId == matchId && q.State == MatchQuestionState.Active, cancellationToken);

    /// <summary>The questions still waiting to be served in a segment, in order.</summary>
    public static Task<List<MatchQuestion>> ReservedInSegmentAsync(
        IAppDbContext db, Guid segmentId, CancellationToken cancellationToken) =>
        db.MatchQuestions
            .Where(q => q.MatchSegmentId == segmentId && q.State == MatchQuestionState.Reserved)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);
}
