using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Qualification;

/// <summary>Which tie-break rule governs a stage. The stage's own rule wins
/// whatever its scope — rules saved through the API carry no scope of their
/// own (the contract has none, so they are stored as MatchRanking) and would
/// otherwise never apply to stage qualification. Failing that, the
/// program-wide rule for the scope; a match-level tie falls back to the
/// program's stage-qualification rule.</summary>
public static class TieBreakRuleResolution
{
    public static TieBreakRule? Resolve(IReadOnlyCollection<TieBreakRule> rules, Guid stageId, TieBreakScope scope)
    {
        var forStage = rules.Where(r => r.StageId == stageId).ToList();
        return forStage.FirstOrDefault(r => r.Scope == scope)
            ?? forStage.OrderBy(r => r.Scope).FirstOrDefault()
            ?? rules.FirstOrDefault(r => r.StageId == null && r.Scope == scope)
            ?? (scope == TieBreakScope.MatchRanking
                ? rules.FirstOrDefault(r => r.StageId == null && r.Scope == TieBreakScope.StageQualification)
                : null);
    }
}
