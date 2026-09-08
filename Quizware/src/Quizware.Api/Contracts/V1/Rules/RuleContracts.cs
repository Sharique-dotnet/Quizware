namespace Quizware.Api.Contracts.V1.Rules;

public sealed record ScoringRuleDto(Guid Id, string FormatCode, string Outcome, string? ContextKey, int Points);

public sealed record UpsertScoringRulesRequest(IReadOnlyList<ScoringRuleDto> Rules);

public sealed record SelectionRuleDto(
    Guid Id,
    string FormatCode,
    Guid? StageId,
    string? DifficultyMixJson,
    string RepeatPolicy,
    string TopicSpreadPolicy,
    string FallbackPolicy);

public sealed record UpsertSelectionRulesRequest(IReadOnlyList<SelectionRuleDto> Rules);

public sealed record SelectionPreviewRequest(Guid StageId, string FormatCode, int QuestionCount);

public sealed record SelectionPreviewResponse(
    int PoolSize,
    int EligibleAfterFilters,
    int EligibleAfterRepeatPolicy,
    IReadOnlyDictionary<string, int> DifficultyMixRequested,
    IReadOnlyDictionary<string, int> DifficultyMixAchievable,
    bool CanSatisfy,
    IReadOnlyList<string> Warnings);

public sealed record QualificationRuleDto(
    Guid Id,
    Guid StageId,
    int WinnersPerMatch,
    int BestRemainingAcrossStage,
    int ManualWildcardSlots);

public sealed record UpsertQualificationRulesRequest(IReadOnlyList<QualificationRuleDto> Rules);

public sealed record TieBreakRuleDto(
    Guid Id,
    Guid StageId,
    IReadOnlyList<string> Criteria,
    string TieBreakFormat,
    int QuestionCount,
    bool SuddenDeath,
    int MaxExtraRounds,
    bool ScoreCountsTowardStage,
    string OnStillTied);

public sealed record UpsertTieBreakRulesRequest(IReadOnlyList<TieBreakRuleDto> Rules);
