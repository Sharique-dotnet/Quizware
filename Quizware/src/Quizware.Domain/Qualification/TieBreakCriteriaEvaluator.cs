namespace Quizware.Domain.Qualification;

/// <summary>A candidate's criterion values, already normalised so that a
/// higher number always means "better" for that criterion.</summary>
public sealed record TieBreakCandidate(Guid TeamId, IReadOnlyDictionary<string, decimal> CriterionValues);

public sealed record TieBreakEvaluationResult(bool IsResolved, string? DecidingCriterion, IReadOnlyList<Guid> TeamIds);

/// <summary>Tries ordered, non-playing criteria before anything is played,
/// cheapest checks first. Narrows the tied group as criteria partially
/// separate it; stops at the first criterion that narrows it to one team.</summary>
public static class TieBreakCriteriaEvaluator
{
    public static TieBreakEvaluationResult Evaluate(
        IReadOnlyList<TieBreakCandidate> candidates, IReadOnlyList<string> orderedCriteria)
    {
        var remaining = candidates;

        foreach (var criterion in orderedCriteria)
        {
            if (remaining.Count <= 1)
            {
                break;
            }

            var best = remaining.Max(c => Value(c, criterion));
            var topGroup = remaining.Where(c => Value(c, criterion) == best).ToList();

            if (topGroup.Count == remaining.Count)
            {
                // This criterion did not separate anyone — try the next one.
                continue;
            }

            if (topGroup.Count == 1)
            {
                return new TieBreakEvaluationResult(true, criterion, topGroup.Select(c => c.TeamId).ToList());
            }

            remaining = topGroup;
        }

        return remaining.Count == 1
            ? new TieBreakEvaluationResult(true, orderedCriteria.LastOrDefault(), remaining.Select(c => c.TeamId).ToList())
            : new TieBreakEvaluationResult(false, null, remaining.Select(c => c.TeamId).ToList());
    }

    private static decimal Value(TieBreakCandidate candidate, string criterion) =>
        candidate.CriterionValues.TryGetValue(criterion, out var value) ? value : 0m;
}
