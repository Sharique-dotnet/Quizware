namespace QuizApp.Api.Contracts.V1.Reports;

public sealed record MatchReportResponse(Guid MatchId, string MatchName, IReadOnlyList<string> Timeline);

public sealed record StageSummaryReportResponse(Guid StageId, string StageName, IReadOnlyList<string> QualificationRecord);

public sealed record TeamPerformanceReportResponse(Guid TeamId, int MatchesPlayed, int TotalScore, double AverageScore);

public sealed record QuestionUsageReportEntry(Guid QuestionId, int TimesUsed, double CorrectRate);

public sealed record QuestionUsageReportResponse(IReadOnlyList<QuestionUsageReportEntry> Entries);

public sealed record AuditLogEntryDto(Guid Id, string EntityName, string EntityId, string Action, string ActorUserId, DateTime OccurredAtUtc);

public sealed record AuditReportResponse(IReadOnlyList<AuditLogEntryDto> Entries);
