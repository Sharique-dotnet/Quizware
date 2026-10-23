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

public sealed record TeamPerformanceReportResponse(Guid TeamId, int MatchesPlayed, int TotalScore, double AverageScore);

public sealed record QuestionUsageReportEntry(Guid QuestionId, int TimesUsed, double CorrectRate);

public sealed record QuestionUsageReportResponse(IReadOnlyList<QuestionUsageReportEntry> Entries);

public sealed record AuditLogEntryDto(Guid Id, string EntityName, string EntityId, string Action, string ActorUserId, DateTime OccurredAtUtc);

public sealed record AuditReportResponse(IReadOnlyList<AuditLogEntryDto> Entries);
