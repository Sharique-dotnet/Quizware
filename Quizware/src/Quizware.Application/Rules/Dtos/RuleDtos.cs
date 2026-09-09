namespace Quizware.Application.Rules.Dtos;

public sealed record ScoringRuleAppDto(Guid Id, string FormatCode, string Outcome, string? ContextKey, int Points);

public sealed record SelectionRuleAppDto(
    Guid Id,
    string FormatCode,
    Guid? StageId,
    string? DifficultyMixJson,
    string RepeatPolicy,
    string TopicSpreadPolicy,
    string FallbackPolicy,
    string? TopicFilterJson = null);

public sealed record QualificationRuleAppDto(
    Guid Id,
    Guid StageId,
    int WinnersPerMatch,
    int BestRemainingAcrossStage,
    int ManualWildcardSlots);

public sealed record TieBreakRuleAppDto(
    Guid Id,
    Guid StageId,
    IReadOnlyList<string> Criteria,
    string TieBreakFormat,
    int QuestionCount,
    bool SuddenDeath,
    int MaxExtraRounds,
    bool ScoreCountsTowardStage,
    string OnStillTied);

public sealed record SelectionPreviewResultDto(
    int PoolSize,
    int EligibleAfterFilters,
    int EligibleAfterRepeatPolicy,
    IReadOnlyDictionary<string, int> DifficultyMixRequested,
    IReadOnlyDictionary<string, int> DifficultyMixAchievable,
    bool CanSatisfy,
    IReadOnlyList<string> Warnings);
