using System.Globalization;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Standings;
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

    internal static string DescribeQualifier(StageReportQualifierDto q) =>
        $"{q.StageRank}. {q.TeamName} — {q.Reason}"
        + (q.SourceMatchName is { } source ? $" ({source})" : string.Empty)
        + $", {q.StageScore} pts, {(q.IsCommitted ? "committed" : "not committed")}"
        + (q.TieBreakProvenance is { } provenance ? $"; {provenance}" : string.Empty);
}
