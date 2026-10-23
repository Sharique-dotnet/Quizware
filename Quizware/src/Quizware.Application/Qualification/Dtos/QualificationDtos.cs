namespace Quizware.Application.Qualification.Dtos;

public sealed record StageRefDto(Guid Id, string Name, int OrderIndex);

public sealed record QualificationRuleSummaryDto(int WinnersPerMatch, int BestRemainingAcrossStage, int ManualWildcardSlots, int TotalSlots);

public sealed record QualifierEntryDto(
    Guid TeamId, string TeamName, Guid? SourceMatchId, string SourceMatch, int StageScore, int StageRank, string Reason, Guid? TieBreakEventId);

public sealed record EliminatedEntryDto(Guid TeamId, string TeamName, int StageScore, int StageRank);

public sealed record TiedTeamDto(Guid TeamId, string TeamName, int StageScore);

public sealed record UnresolvedTieSummaryDto(
    int Rank, IReadOnlyList<TiedTeamDto> Teams, IReadOnlyList<string> CriteriaApplied, bool StillTied, IReadOnlyList<string> ResolutionOptions);

public sealed record QualificationPreviewDto(
    StageRefDto FromStage,
    StageRefDto? ToStage,
    QualificationRuleSummaryDto Rule,
    bool AllMatchesComplete,
    IReadOnlyList<QualifierEntryDto> Qualifiers,
    IReadOnlyList<EliminatedEntryDto> Eliminated,
    IReadOnlyList<UnresolvedTieSummaryDto> UnresolvedTies,
    bool CanCommit,
    string? BlockedReason);

public sealed record TieTeamEntryDto(Guid TeamId, string TeamName, int EnteringScore);

public sealed record TieCriterionEntryDto(string Criterion, bool Separated, IReadOnlyList<int>? Values, string? Reason);

public sealed record TieBreakRuleSummaryDto(
    string TieBreakFormat, int QuestionCount, IReadOnlyList<int> DifficultyRange, bool SuddenDeath, int MaxExtraRounds,
    bool ScoreCountsTowardStage, string OnStillTied);

public sealed record TieEntryDto(
    Guid TieBreakEventId,
    string State,
    int ContestedRank,
    int ContestedSlots,
    bool AffectsQualification,
    IReadOnlyList<TieTeamEntryDto> Teams,
    IReadOnlyList<TieCriterionEntryDto> CriteriaApplied,
    TieBreakRuleSummaryDto Rule,
    IReadOnlyList<string> AvailableActions);

public sealed record StageTiesDto(Guid StageId, IReadOnlyList<TieEntryDto> Ties, bool BlocksCommit);
