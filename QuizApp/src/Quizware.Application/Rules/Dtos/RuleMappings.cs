using Quizware.Domain.Qualification;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Rules.Dtos;

internal static class RuleMappings
{
    public static ScoringRuleAppDto ToDto(this ScoringRule rule) =>
        new(rule.Id, rule.FormatCode.ToString(), rule.Outcome.ToString(), rule.ContextKey, rule.Points);

    public static SelectionRuleAppDto ToDto(this QuestionSelectionRule rule) =>
        new(rule.Id, rule.FormatCode.ToString(), rule.StageId, rule.DifficultyMixJson,
            rule.RepeatPolicy.ToString(), rule.TopicSpreadPolicy.ToString(), rule.FallbackPolicy.ToString());

    public static QualificationRuleAppDto ToDto(this QualificationRule rule) =>
        new(rule.Id, rule.FromStageId, rule.WinnersPerMatch, rule.BestRemainingAcrossStage, rule.ManualWildcardSlots);

    public static TieBreakRuleAppDto ToDto(this TieBreakRule rule) =>
        new(rule.Id, rule.StageId ?? Guid.Empty, rule.Criteria, rule.TieBreakFormatCode.ToString(), rule.QuestionCount,
            rule.SuddenDeath, rule.MaxExtraRounds, rule.ScoreCountsTowardStage, rule.OnStillTied.ToString());
}
