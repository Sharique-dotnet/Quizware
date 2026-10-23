using System.Globalization;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Standings;
using Quizware.Application.Abstractions;
using Quizware.Application.Reporting.Dtos;

namespace Quizware.Api.Contracts.V1.Reports;

/// <summary>Maps the Application's report DTOs to the API contracts.</summary>
public static class ReportMapper
{
    public static MatchReportResponse ToResponse(MatchReportDto dto) =>
        new(
            dto.MatchId,
            dto.MatchName,
            dto.Timeline
                .Select(e => $"#{e.SequenceNumber} {e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC {e.EventType}")
                .ToList(),
            dto.MatchNumber,
            dto.MatchKind,
            dto.StageId,
            dto.StageName,
            dto.State,
            dto.StartedAtUtc,
            dto.CompletedAtUtc,
            dto.WinnerTeamId,
            dto.WinnerTeamName,
            dto.IsTied,
            dto.AbandonReason,
            dto.Teams
                .Select(t => new MatchReportTeam(
                    t.TeamId, t.TeamName, t.SeatNumber, t.Status, t.Score, t.Rank,
                    t.CorrectCount, t.IncorrectCount, t.NoAnswerCount, t.PassedCount, t.ExcludedFromStandings))
                .ToList(),
            dto.Questions
                .Select(q => new MatchReportQuestion(
                    q.MatchQuestionId, q.QuestionId, q.Format, q.SegmentOrder, q.OrderIndex, q.QuestionText, q.State, q.ServedAtUtc,
                    q.Answers
                        .Select(a => new MatchReportAnswer(a.TeamId, a.TeamName, a.Outcome, a.IsCorrect, a.PassNumber, a.IsReversed, a.AnsweredAtUtc))
                        .ToList()))
                .ToList(),
            dto.Timeline.Select(e => new MatchEventDto(e.SequenceNumber, e.EventType, e.Detail, e.OccurredAtUtc)).ToList());

    public static StageSummaryReportResponse ToResponse(StageSummaryReportDto dto) =>
        new(
            dto.StageId,
            dto.StageName,
            dto.Qualifiers.Select(DescribeQualifier).ToList(),
            dto.StageType,
            dto.State,
            dto.OrderIndex,
            dto.StartedAtUtc,
            dto.CompletedAtUtc,
            dto.Matches
                .Select(m => new StageReportMatch(m.MatchId, m.MatchName, m.MatchKind, m.State, m.WinnerTeamId, m.WinnerTeamName, m.IsTied))
                .ToList(),
            dto.Standings.Standings.Select(s => new StandingEntry(s.TeamId, s.TeamName, s.Score, s.Rank)).ToList(),
            dto.Standings.TieBreakNotes,
            dto.Qualifiers
                .Select(q => new StageReportQualifier(
                    q.TeamId, q.TeamName, q.Reason, q.StageScore, q.StageRank, q.IsCommitted, q.ToStageId,
                    q.SourceMatchId, q.SourceMatchName, q.TieBreakEventId, q.TieBreakProvenance))
                .ToList(),
            dto.TieBreaks
                .Select(t => new StageReportTieBreak(
                    t.TieBreakEventId, t.Scope, t.State, t.ContestedRank, t.ContestedSlots, t.ResolutionMethod, t.ResolvedByCriterion,
                    t.TieBreakMatchId, t.RoundsPlayed, t.DetectedAtUtc, t.ResolvedAtUtc, t.Notes,
                    t.Participants
                        .Select(p => new StageReportTieParticipant(p.TeamId, p.TeamName, p.EnteringScore, p.TieBreakPoints, p.ResultRank, p.Qualified))
                        .ToList()))
                .ToList());

    public static TeamPerformanceReportResponse ToResponse(TeamPerformanceReportDto dto) =>
        new(
            dto.TeamId,
            dto.MatchesPlayed,
            dto.TotalScore,
            dto.AverageScore,
            dto.TeamName,
            dto.Status,
            dto.Wins,
            dto.CorrectCount,
            dto.IncorrectCount,
            dto.NoAnswerCount,
            dto.PassedCount,
            dto.Accuracy,
            dto.OverallRank,
            dto.Matches
                .Select(m => new TeamPerformanceMatch(
                    m.MatchId, m.MatchName, m.StageId, m.StageName, m.MatchKind, m.State, m.Score, m.Rank, m.Won, m.CorrectCount, m.IncorrectCount))
                .ToList(),
            dto.Formats.Select(f => new TeamFormatPerformance(f.Format, f.Attempts, f.CorrectCount, f.Points)).ToList(),
            dto.Qualifications
                .Select(q => new TeamQualificationEntry(
                    q.FromStageId, q.FromStageName, q.Reason, q.StageScore, q.StageRank, q.IsCommitted, q.TieBreakProvenance))
                .ToList());

    public static QuestionUsageReportResponse ToResponse(QuestionUsageReportDto dto) =>
        new(
            dto.Entries
                .Select(e => new QuestionUsageReportEntry(
                    e.QuestionId, e.TimesUsed, e.CorrectRate, e.QuestionText, e.Format, e.Difficulty, e.MatchesUsed, e.Attempts,
                    e.CorrectCount, e.IncorrectCount, e.EveryoneCorrect, e.NobodyCorrect, e.Flags, e.LastUsedAtUtc))
                .ToList(),
            dto.TotalQuestions,
            dto.FlaggedCount);

    public static AuditReportResponse ToResponse(AuditLogPage page) =>
        new(
            page.Entries
                .Select(e => new AuditLogEntryDto(
                    e.Id, e.EntityName, e.EntityId, e.Action, e.UserId?.ToString() ?? string.Empty, e.OccurredAtUtc,
                    e.UserName, e.ChangedColumns, e.OldValuesJson, e.NewValuesJson, e.CorrelationId))
                .ToList(),
            page.TotalCount,
            page.Page,
            page.PageSize);

    internal static string DescribeQualifier(StageReportQualifierDto q) =>
        $"{q.StageRank}. {q.TeamName} — {q.Reason}"
        + (q.SourceMatchName is { } source ? $" ({source})" : string.Empty)
        + $", {q.StageScore} pts, {(q.IsCommitted ? "committed" : "not committed")}"
        + (q.TieBreakProvenance is { } provenance ? $"; {provenance}" : string.Empty);
}
