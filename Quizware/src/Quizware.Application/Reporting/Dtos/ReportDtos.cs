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

public sealed record TeamPerformanceMatchDto(
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

public sealed record TeamFormatPerformanceDto(string Format, int Attempts, int CorrectCount, int Points);

public sealed record TeamQualificationDto(
    Guid FromStageId, string FromStageName, string Reason, int StageScore, int StageRank, bool IsCommitted, string? TieBreakProvenance);

public sealed record TeamPerformanceReportDto(
    Guid TeamId,
    string TeamName,
    string Status,
    int MatchesPlayed,
    int TotalScore,
    double AverageScore,
    int Wins,
    int CorrectCount,
    int IncorrectCount,
    int NoAnswerCount,
    int PassedCount,
    double Accuracy,
    int OverallRank,
    IReadOnlyList<TeamPerformanceMatchDto> Matches,
    IReadOnlyList<TeamFormatPerformanceDto> Formats,
    IReadOnlyList<TeamQualificationDto> Qualifications);

public sealed record QuestionUsageEntryDto(
    Guid QuestionId,
    string? QuestionText,
    string Format,
    string Difficulty,
    int TimesUsed,
    int MatchesUsed,
    int Attempts,
    int CorrectCount,
    int IncorrectCount,
    double CorrectRate,
    bool EveryoneCorrect,
    bool NobodyCorrect,
    IReadOnlyList<string> Flags,
    DateTime LastUsedAtUtc);

public sealed record QuestionUsageReportDto(int TotalQuestions, int FlaggedCount, IReadOnlyList<QuestionUsageEntryDto> Entries);
