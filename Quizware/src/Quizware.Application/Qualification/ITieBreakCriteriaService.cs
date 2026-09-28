namespace Quizware.Application.Qualification;

/// <summary>One criterion's pass over the tied teams: each team's value
/// (normalised so higher is better), whether it told any of them apart, and
/// why it could not when there was nothing to compare.</summary>
public sealed record CriterionTrace(string Criterion, bool Separated, IReadOnlyDictionary<Guid, decimal> Values, string? Note);

/// <summary><see cref="RankedGroups"/> orders the tied teams best first; a
/// group of more than one is still level after every criterion.
/// <see cref="DecidingCriterion"/> is the criterion that settled first place
/// (null if nothing did).</summary>
public sealed record TieBreakOrdering(
    IReadOnlyList<IReadOnlyList<Guid>> RankedGroups, string? DecidingCriterion, IReadOnlyList<CriterionTrace> Trace)
{
    public bool IsFullyResolved => RankedGroups.All(g => g.Count == 1);
}

/// <summary>P10-07: orders teams level on stage points using the stage's
/// ordered, non-playing tie-break criteria (wrapping
/// <see cref="Domain.Qualification.TieBreakCriteriaEvaluator"/>), and records
/// which criterion decided. Playing a tie-break match is Phase 11's job.</summary>
public interface ITieBreakCriteriaService
{
    Task<IReadOnlyList<string>> CriteriaForStageAsync(Guid stageId, CancellationToken cancellationToken);

    Task<TieBreakOrdering> OrderTiedTeamsAsync(Guid stageId, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken);
}
