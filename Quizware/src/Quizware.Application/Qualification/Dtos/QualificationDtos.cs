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
