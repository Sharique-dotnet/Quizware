using System.Globalization;
using Quizware.Application.Abstractions;
using Quizware.Application.Reporting.Dtos;

namespace Quizware.Application.Reporting;

/// <summary>A report flattened into titled tables — the format-neutral shape
/// every exporter (Excel, CSV, PDF) renders. Cells are already text, so each
/// format shows exactly the same values.</summary>
public sealed record ReportDocument(string Title, IReadOnlyList<ReportSection> Sections);

public sealed record ReportSection(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Builds a <see cref="ReportDocument"/> from each report DTO.</summary>
public static class ReportDocuments
{
    public static ReportDocument Match(MatchReportDto r) =>
        new($"Match report — {r.MatchName}",
        [
            Summary(
                ("Match", r.MatchName), ("Stage", r.StageName), ("Kind", r.MatchKind), ("State", r.State),
                ("Started (UTC)", Time(r.StartedAtUtc)), ("Completed (UTC)", Time(r.CompletedAtUtc)),
                ("Winner", r.WinnerTeamName ?? string.Empty), ("Tied", YesNo(r.IsTied)), ("Abandon reason", r.AbandonReason ?? string.Empty)),
            new("Teams",
                ["Rank", "Team", "Seat", "Status", "Score", "Correct", "Incorrect", "No answer", "Passed", "Excluded from standings"],
                r.Teams.Select(t => Row(
                    Number(t.Rank), t.TeamName, Number(t.SeatNumber), t.Status, Number(t.Score), Number(t.CorrectCount),
                    Number(t.IncorrectCount), Number(t.NoAnswerCount), Number(t.PassedCount), YesNo(t.ExcludedFromStandings))).ToList()),
            new("Questions",
                ["Segment", "Order", "Format", "Question", "State", "Answers"],
                r.Questions.Select(q => Row(
                    Number(q.SegmentOrder + 1), Number(q.OrderIndex + 1), q.Format, q.QuestionText ?? string.Empty, q.State,
                    string.Join("; ", q.Answers.Select(a => $"{a.TeamName}: {a.Outcome}{(a.IsReversed ? " (reversed)" : string.Empty)}")))).ToList()),
            new("Timeline",
                ["Sequence", "Time (UTC)", "Event", "Detail"],
                r.Timeline.Select(e => Row(Number(e.SequenceNumber), Time(e.OccurredAtUtc), e.EventType, e.Detail ?? string.Empty)).ToList()),
        ]);

    public static ReportDocument Stage(StageSummaryReportDto r) =>
        new($"Stage summary — {r.StageName}",
        [
            Summary(
                ("Stage", r.StageName), ("Type", r.StageType), ("State", r.State), ("Order", Number(r.OrderIndex)),
                ("Started (UTC)", Time(r.StartedAtUtc)), ("Completed (UTC)", Time(r.CompletedAtUtc))),
            new("Standings",
                ["Rank", "Team", "Score"],
                r.Standings.Standings.Select(s => Row(Number(s.Rank), s.TeamName, Number(s.Score))).ToList()),
            new("Tie-break notes", ["Note"], r.Standings.TieBreakNotes.Select(n => Row(n)).ToList()),
            new("Matches",
                ["Match", "Kind", "State", "Winner", "Tied"],
                r.Matches.Select(m => Row(m.MatchName, m.MatchKind, m.State, m.WinnerTeamName ?? string.Empty, YesNo(m.IsTied))).ToList()),
            new("Qualifiers",
                ["Rank", "Team", "Reason", "Score", "Source match", "Committed", "Tie-break provenance"],
                r.Qualifiers.Select(q => Row(
                    Number(q.StageRank), q.TeamName, q.Reason, Number(q.StageScore), q.SourceMatchName ?? string.Empty,
                    YesNo(q.IsCommitted), q.TieBreakProvenance ?? string.Empty)).ToList()),
            new("Tie-breaks",
                ["Detected (UTC)", "Scope", "State", "Contested rank", "Slots", "Resolution", "Criterion", "Teams"],
                r.TieBreaks.Select(t => Row(
                    Time(t.DetectedAtUtc), t.Scope, t.State, Number(t.ContestedRank), Number(t.ContestedSlots),
                    t.ResolutionMethod ?? string.Empty, t.ResolvedByCriterion ?? string.Empty,
                    string.Join("; ", t.Participants.Select(p =>
                        $"{p.TeamName} ({p.EnteringScore}{(p.ResultRank is { } rank ? $", #{rank}" : string.Empty)}{(p.Qualified ? ", qualified" : string.Empty)})")))).ToList()),
        ]);

    public static ReportDocument Team(TeamPerformanceReportDto r) =>
        new($"Team performance — {r.TeamName}",
        [
            Summary(
                ("Team", r.TeamName), ("Status", r.Status), ("Matches played", Number(r.MatchesPlayed)), ("Total score", Number(r.TotalScore)),
                ("Average score", Amount(r.AverageScore)), ("Wins", Number(r.Wins)), ("Correct", Number(r.CorrectCount)),
                ("Incorrect", Number(r.IncorrectCount)), ("No answer", Number(r.NoAnswerCount)), ("Passed", Number(r.PassedCount)),
                ("Accuracy", Percent(r.Accuracy)), ("Overall rank", Number(r.OverallRank))),
            new("Matches",
                ["Match", "Stage", "Kind", "State", "Score", "Rank", "Won", "Correct", "Incorrect"],
                r.Matches.Select(m => Row(
                    m.MatchName, m.StageName, m.MatchKind, m.State, Number(m.Score), Number(m.Rank), YesNo(m.Won),
                    Number(m.CorrectCount), Number(m.IncorrectCount))).ToList()),
            new("Formats",
                ["Format", "Attempts", "Correct", "Points"],
                r.Formats.Select(f => Row(f.Format, Number(f.Attempts), Number(f.CorrectCount), Number(f.Points))).ToList()),
            new("Qualifications",
                ["From stage", "Reason", "Stage score", "Stage rank", "Committed", "Tie-break provenance"],
                r.Qualifications.Select(q => Row(
                    q.FromStageName, q.Reason, Number(q.StageScore), Number(q.StageRank), YesNo(q.IsCommitted), q.TieBreakProvenance ?? string.Empty)).ToList()),
        ]);

    public static ReportDocument QuestionUsage(QuestionUsageReportDto r) =>
        new("Question usage",
        [
            Summary(("Questions", Number(r.TotalQuestions)), ("Flagged", Number(r.FlaggedCount))),
            new("Questions",
                ["Question", "Format", "Difficulty", "Times used", "Matches", "Attempts", "Correct", "Incorrect", "Correct rate", "Flags", "Last used (UTC)"],
                r.Entries.Select(e => Row(
                    e.QuestionText ?? e.QuestionId.ToString(), e.Format, e.Difficulty, Number(e.TimesUsed), Number(e.MatchesUsed),
                    Number(e.Attempts), Number(e.CorrectCount), Number(e.IncorrectCount), Percent(e.CorrectRate),
                    string.Join(", ", e.Flags), Time(e.LastUsedAtUtc))).ToList()),
        ]);

    public static ReportDocument Audit(AuditLogPage page) =>
        new("Audit log",
        [
            new("Audit log",
                ["Time (UTC)", "Entity", "Entity id", "Action", "Actor", "Changed columns", "Old values", "New values"],
                page.Entries.Select(e => Row(
                    Time(e.OccurredAtUtc), e.EntityName, e.EntityId, e.Action, e.UserName ?? e.UserId?.ToString() ?? string.Empty,
                    e.ChangedColumns ?? string.Empty, e.OldValuesJson ?? string.Empty, e.NewValuesJson ?? string.Empty)).ToList()),
        ]);

    private static ReportSection Summary(params (string Field, string Value)[] fields) =>
        new("Summary", ["Field", "Value"], fields.Select(f => Row(f.Field, f.Value)).ToList());

    private static IReadOnlyList<string> Row(params string[] cells) => cells;

    private static string Time(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Amount(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Percent(double ratio) => ratio.ToString("0.##%", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "Yes" : "No";
}
