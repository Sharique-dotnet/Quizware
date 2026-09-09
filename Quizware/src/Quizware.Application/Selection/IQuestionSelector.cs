namespace Quizware.Application.Selection;

/// <summary>Phase 8: the question draw. Pool building, repeat-policy
/// exclusion, difficulty-mix splitting, seeded weighted random draw, topic
/// spread, and the widen-then-fail fallback ladder all live here so Phase 9's
/// match-start handler and the P7 selection-rule preview screen share one
/// implementation rather than two.</summary>
public interface IQuestionSelector
{
    /// <summary>Dry run: reports what the draw would produce without
    /// reserving anything. Never throws on exhaustion — reports
    /// <see cref="SelectionResult.CanSatisfy"/> = false with warnings
    /// instead, since a preview must never fail loudly mid-configuration.</summary>
    Task<SelectionResult> PreviewAsync(SelectionRequest request, CancellationToken cancellationToken);

    /// <summary>The real draw: reserves every drawn question into
    /// <c>MatchQuestion</c> (State = Reserved) for the given match/segment,
    /// so a crash after this call changes nothing about what comes next. Adds
    /// to the tracked <c>IAppDbContext</c> but does not call
    /// <c>SaveChangesAsync</c> — the caller (Phase 9's match-start handler)
    /// owns the transaction boundary across every segment's reservation.
    /// Throws <see cref="Quizware.Domain.Common.Exceptions.QuestionPoolExhaustedException"/>
    /// (QUESTION_POOL_EXHAUSTED) if the fallback ladder still cannot meet
    /// <see cref="SelectionRequest.QuestionCount"/>.</summary>
    Task<SelectionResult> SelectAndReserveAsync(
        SelectionRequest request, Guid matchSegmentId, string reservedBy, CancellationToken cancellationToken);

    /// <summary>Releases every still-Reserved <c>MatchQuestion</c> for a match
    /// (State -> Released), freeing those questions back to the pool for
    /// other matches. Used when a match is abandoned before it started
    /// serving. Does not call <c>SaveChangesAsync</c>.</summary>
    Task ReleaseReservationsAsync(Guid matchId, CancellationToken cancellationToken);
}
