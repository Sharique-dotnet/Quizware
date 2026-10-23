using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Scoring.Dtos;

namespace Quizware.Application.Reporting.Dtos;

public sealed record MatchReportTeamDto(
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

public sealed record MatchReportAnswerDto(
    Guid TeamId, string TeamName, string Outcome, bool? IsCorrect, int PassNumber, bool IsReversed, DateTime AnsweredAtUtc);

public sealed record MatchReportQuestionDto(
    Guid MatchQuestionId,
    Guid QuestionId,
    string Format,
    int SegmentOrder,
    int OrderIndex,
    string? QuestionText,
    string State,
    DateTime? ServedAtUtc,
    IReadOnlyList<MatchReportAnswerDto> Answers);

public sealed record MatchReportDto(
    Guid MatchId,
    string MatchName,
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
    IReadOnlyList<MatchReportTeamDto> Teams,
    IReadOnlyList<MatchReportQuestionDto> Questions,
    IReadOnlyList<MatchEventItemDto> Timeline);

public sealed record StageReportMatchDto(
    Guid MatchId, string MatchName, string MatchKind, string State, Guid? WinnerTeamId, string? WinnerTeamName, bool IsTied);

public sealed record StageReportQualifierDto(
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

public sealed record StageReportTieParticipantDto(
    Guid TeamId, string TeamName, int EnteringScore, int TieBreakPoints, int? ResultRank, bool Qualified);

public sealed record StageReportTieBreakDto(
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
    IReadOnlyList<StageReportTieParticipantDto> Participants);

public sealed record StageSummaryReportDto(
    Guid StageId,
    string StageName,
    string StageType,
    string State,
    int OrderIndex,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<StageReportMatchDto> Matches,
    StageStandingsDto Standings,
    IReadOnlyList<StageReportQualifierDto> Qualifiers,
    IReadOnlyList<StageReportTieBreakDto> TieBreaks);
