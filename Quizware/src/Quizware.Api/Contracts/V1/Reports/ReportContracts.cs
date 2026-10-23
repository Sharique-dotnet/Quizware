using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Standings;

namespace Quizware.Api.Contracts.V1.Reports;

/// <summary>P13-01. <see cref="Timeline"/> is the original one-line-per-event
/// rendering; <see cref="Events"/> carries the same MatchEvent rows in full.
/// Fields after <see cref="Timeline"/> were added in Phase 13.</summary>
public sealed record MatchReportResponse(
    Guid MatchId,
    string MatchName,
    IReadOnlyList<string> Timeline,
    int MatchNumber,
    string MatchKind,
    Guid StageId,
    string StageName,
    string State,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    Guid? WinnerTeamId,
    string? WinnerTeamName,
    bool IsTied,
    string? AbandonReason,
    IReadOnlyList<MatchReportTeam> Teams,
    IReadOnlyList<MatchReportQuestion> Questions,
    IReadOnlyList<MatchEventDto> Events);

public sealed record MatchReportTeam(
    Guid TeamId,
    string TeamName,
    int SeatNumber,
    string Status,
    int Score,
    int? Rank,
    int CorrectCount,
    int IncorrectCount,
    int NoAnswerCount,
    int PassedCount,
    bool ExcludedFromStandings);

public sealed record MatchReportQuestion(
    Guid MatchQuestionId,
    Guid QuestionId,
    string Format,
    int SegmentOrder,
    int OrderIndex,
    string? QuestionText,
    string State,
    DateTime? ServedAtUtc,
    IReadOnlyList<MatchReportAnswer> Answers);

public sealed record MatchReportAnswer(
    Guid TeamId, string TeamName, string Outcome, bool? IsCorrect, int PassNumber, bool IsReversed, DateTime AnsweredAtUtc);

/// <summary>P13-02. <see cref="QualificationRecord"/> is the original
/// one-line-per-qualifier rendering; <see cref="Qualifiers"/> and
/// <see cref="TieBreaks"/> carry the detail, including tie-break provenance.</summary>
public sealed record StageSummaryReportResponse(
    Guid StageId,
    string StageName,
    IReadOnlyList<string> QualificationRecord,
    string StageType,
    string State,
    int OrderIndex,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<StageReportMatch> Matches,
    IReadOnlyList<StandingEntry> Standings,
    IReadOnlyList<string> TieBreakNotes,
    IReadOnlyList<StageReportQualifier> Qualifiers,
    IReadOnlyList<StageReportTieBreak> TieBreaks);

public sealed record StageReportMatch(
    Guid MatchId, string MatchName, string MatchKind, string State, Guid? WinnerTeamId, string? WinnerTeamName, bool IsTied);

public sealed record StageReportQualifier(
    Guid TeamId,
    string TeamName,
    string Reason,
    int StageScore,
    int StageRank,
    bool IsCommitted,
    Guid? ToStageId,
    Guid? SourceMatchId,
    string? SourceMatchName,
    Guid? TieBreakEventId,
    string? TieBreakProvenance);

public sealed record StageReportTieBreak(
    Guid TieBreakEventId,
    string Scope,
    string State,
    int ContestedRank,
    int ContestedSlots,
    string? ResolutionMethod,
    string? ResolvedByCriterion,
    Guid? TieBreakMatchId,
    int RoundsPlayed,
    DateTime DetectedAtUtc,
    DateTime? ResolvedAtUtc,
    string? Notes,
    IReadOnlyList<StageReportTieParticipant> Participants);

public sealed record StageReportTieParticipant(
    Guid TeamId, string TeamName, int EnteringScore, int TieBreakPoints, int? ResultRank, bool Qualified);

/// <summary>P13-03. The totals count completed matches only; <see cref="Matches"/>
/// lists every match past setup. Fields after <see cref="AverageScore"/> were
/// added in Phase 13.</summary>
public sealed record TeamPerformanceReportResponse(
    Guid TeamId,
    int MatchesPlayed,
    int TotalScore,
    double AverageScore,
    string TeamName,
    string Status,
    int Wins,
    int CorrectCount,
    int IncorrectCount,
    int NoAnswerCount,
    int PassedCount,
    double Accuracy,
    int OverallRank,
    IReadOnlyList<TeamPerformanceMatch> Matches,
    IReadOnlyList<TeamFormatPerformance> Formats,
    IReadOnlyList<TeamQualificationEntry> Qualifications);

public sealed record TeamPerformanceMatch(
    Guid MatchId,
    string MatchName,
    Guid StageId,
    string StageName,
    string MatchKind,
    string State,
    int Score,
    int? Rank,
    bool Won,
    int CorrectCount,
    int IncorrectCount);

public sealed record TeamFormatPerformance(string Format, int Attempts, int CorrectCount, int Points);

public sealed record TeamQualificationEntry(
    Guid FromStageId, string FromStageName, string Reason, int StageScore, int StageRank, bool IsCommitted, string? TieBreakProvenance);

/// <summary>P13-04. <see cref="EveryoneCorrect"/> marks a question every
/// judged answer got right. Fields after <see cref="CorrectRate"/> were added in Phase 13.</summary>
public sealed record QuestionUsageReportEntry(
    Guid QuestionId,
    int TimesUsed,
    double CorrectRate,
    string? QuestionText,
    string Format,
    string Difficulty,
    int MatchesUsed,
    int Attempts,
    int CorrectCount,
    int IncorrectCount,
    bool EveryoneCorrect,
    bool NobodyCorrect,
    IReadOnlyList<string> Flags,
    DateTime LastUsedAtUtc);

public sealed record QuestionUsageReportResponse(IReadOnlyList<QuestionUsageReportEntry> Entries, int TotalQuestions, int FlaggedCount);

/// <summary>P13-05. <see cref="Id"/> is the AuditLog row's bigint key (it was
/// declared a Guid while the endpoint was a stub, which the table never was).
/// <see cref="ActorUserId"/> is empty for writes made without a signed-in user.
/// Fields after <see cref="OccurredAtUtc"/> were added in Phase 13.</summary>
public sealed record AuditLogEntryDto(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    string ActorUserId,
    DateTime OccurredAtUtc,
    string? ActorName,
    string? ChangedColumns,
    string? OldValuesJson,
    string? NewValuesJson,
    string? CorrelationId);

public sealed record AuditReportResponse(IReadOnlyList<AuditLogEntryDto> Entries, int TotalCount, int Page, int PageSize);
