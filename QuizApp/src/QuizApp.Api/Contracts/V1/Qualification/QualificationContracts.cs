namespace QuizApp.Api.Contracts.V1.Qualification;

public sealed record QualifierDto(Guid TeamId, string TeamName, string SourceMatch, int StageScore, int StageRank, string Reason);

public sealed record EliminatedTeamDto(Guid TeamId, string TeamName, int StageScore, int StageRank);

public sealed record UnresolvedTieTeamDto(Guid TeamId, string TeamName, int StageScore);

public sealed record UnresolvedTieDto(
    int Rank,
    IReadOnlyList<UnresolvedTieTeamDto> Teams,
    IReadOnlyList<string> CriteriaApplied,
    bool StillTied,
    IReadOnlyList<string> ResolutionOptions);

public sealed record StageRefDto(Guid Id, string Name, int OrderIndex);

public sealed record QualificationRulePreviewDto(int WinnersPerMatch, int BestRemainingAcrossStage, int ManualWildcardSlots, int TotalSlots);

public sealed record QualificationPreviewResponse(
    StageRefDto FromStage,
    StageRefDto? ToStage,
    QualificationRulePreviewDto Rule,
    bool AllMatchesComplete,
    IReadOnlyList<QualifierDto> Qualifiers,
    IReadOnlyList<EliminatedTeamDto> Eliminated,
    IReadOnlyList<UnresolvedTieDto> UnresolvedTies,
    bool CanCommit,
    string? BlockedReason);

public sealed record SetWildcardsRequest(IReadOnlyList<Guid> TeamIds);

public sealed record CommitQualificationResponse(int MatchesCreated);

public sealed record TieTeamDto(Guid TeamId, string TeamName, int EnteringScore);

public sealed record TieCriterionResultDto(string Criterion, bool Separated, IReadOnlyList<int>? Values, string? Reason);

public sealed record TieBreakRulePreviewDto(
    string TieBreakFormat,
    int QuestionCount,
    IReadOnlyList<int> DifficultyRange,
    bool SuddenDeath,
    int MaxExtraRounds,
    bool ScoreCountsTowardStage,
    string OnStillTied);

public sealed record TieDto(
    Guid TieBreakEventId,
    string State,
    int ContestedRank,
    int ContestedSlots,
    bool AffectsQualification,
    IReadOnlyList<TieTeamDto> Teams,
    IReadOnlyList<TieCriterionResultDto> CriteriaApplied,
    TieBreakRulePreviewDto Rule,
    IReadOnlyList<string> AvailableActions);

public sealed record TiesResponse(Guid StageId, IReadOnlyList<TieDto> Ties, bool BlocksCommit);

public sealed record CreateTieBreakMatchRequest(string? FormatCodeOverride, int? QuestionCountOverride, bool? SuddenDeathOverride, string? Reason);

public sealed record TieBreakParticipantDto(Guid TeamId, string TeamName, int SeatNumber, int TurnOrder);

public sealed record TieBreakSegmentDto(Guid SegmentId, string Format, int OrderIndex, int PlannedQuestionCount, bool IsSuddenDeath);

public sealed record CreateTieBreakMatchResponse(
    Guid TieBreakEventId,
    Guid MatchId,
    string MatchKind,
    Guid StageId,
    int MatchNumber,
    string Name,
    string State,
    IReadOnlyList<TieBreakParticipantDto> Participants,
    IReadOnlyList<TieBreakSegmentDto> Segments,
    int QuestionsReserved,
    bool ScoreCountsTowardStage,
    string NextStep);

public sealed record ResolveTieManuallyRequest(IReadOnlyList<Guid> RankedTeamIds, string Method, string Reason, Guid ApprovedByUserId);

public sealed record AbandonTieRequest(string Reason);
