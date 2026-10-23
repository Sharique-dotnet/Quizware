using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Qualification;

/// <summary>Turns a <see cref="QualificationEvaluation"/> into the preview the API returns.</summary>
public static class QualificationPreviewMapper
{
    public static QualificationPreviewDto ToPreview(QualificationEvaluation evaluation)
    {
        var standing = evaluation.Standing;
        var rule = evaluation.Rule;

        return new QualificationPreviewDto(
            ToRef(evaluation.FromStage),
            evaluation.ToStage is null ? null : ToRef(evaluation.ToStage),
            new QualificationRuleSummaryDto(
                rule.WinnersPerMatch,
                rule.BestRemainingAcrossStage,
                rule.ManualWildcardSlots,
                (evaluation.RegularMatchCount * rule.WinnersPerMatch) + rule.BestRemainingAcrossStage + rule.ManualWildcardSlots),
            evaluation.AllMatchesComplete,
            evaluation.Qualifiers
                .Select(q => new QualifierEntryDto(
                    q.TeamId,
                    standing[q.TeamId].TeamName,
                    q.SourceMatchId,
                    MatchName(evaluation.MatchOfTeam.GetValueOrDefault(q.TeamId)),
                    standing[q.TeamId].Score,
                    standing[q.TeamId].Rank,
                    q.Reason.ToString(),
                    q.TieBreakEventId))
                .ToList(),
            evaluation.Eliminated
                .Select(id => new EliminatedEntryDto(id, standing[id].TeamName, standing[id].Score, standing[id].Rank))
                .ToList(),
            evaluation.UnresolvedBlockingTies
                .Select(t => new UnresolvedTieSummaryDto(
                    t.Tie.ContestedRank,
                    t.Contested.Select(id => new TiedTeamDto(id, standing[id].TeamName, standing[id].Score)).ToList(),
                    t.Criteria.Trace.Select(c => c.Criterion).ToList(),
                    StillTied: true,
                    ResolutionOptions(evaluation.TieBreakRule)))
                .ToList(),
            evaluation.CanCommit,
            evaluation.Blockers.Count == 0 ? null : string.Join(" ", evaluation.Blockers));
    }

    public static StageRefDto ToRef(Stage stage) => new(stage.Id, stage.Name, stage.OrderIndex);

    public static string MatchName(Match? match) => match is null ? string.Empty : match.Name ?? $"Match {match.MatchNumber}";

    /// <summary>What may settle a tie the criteria could not: a tie-break
    /// match (when the rule plays one) and a manual decision always, plus
    /// the rule's own still-tied fallback.</summary>
    public static IReadOnlyList<string> ResolutionOptions(TieBreakRule? rule)
    {
        var options = new List<string>();
        if (rule?.PlayTieBreakSegment ?? true)
        {
            options.Add("TieBreakerMatch");
        }

        options.Add("ManualDecision");
        if (rule?.OnStillTied == OnStillTiedPolicy.CoinToss)
        {
            options.Add("CoinToss");
        }

        if (rule?.OnStillTied == OnStillTiedPolicy.ShareTheSlot)
        {
            options.Add("ShareTheSlot");
        }

        return options;
    }
}
