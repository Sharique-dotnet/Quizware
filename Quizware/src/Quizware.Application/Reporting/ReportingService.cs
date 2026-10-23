using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Reporting.Dtos;
using Quizware.Application.Scoring;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Reporting;

/// <summary>Phase 13: read-only reports assembled from the read models
/// (TeamMatchScore, TeamStageScore), the MatchEvent timeline and the
/// qualification records — never a write, never a recount of scores.</summary>
public interface IReportingService
{
    Task<MatchReportDto> MatchAsync(Guid programId, Guid matchId, CancellationToken cancellationToken);

    Task<StageSummaryReportDto> StageAsync(Guid programId, Guid stageId, CancellationToken cancellationToken);

    Task<TeamPerformanceReportDto> TeamAsync(Guid programId, Guid teamId, CancellationToken cancellationToken);

    Task<QuestionUsageReportDto> QuestionUsageAsync(Guid programId, Guid? stageId, bool flaggedOnly, CancellationToken cancellationToken);
}

public sealed class ReportingService : IReportingService
{
    /// <summary>Outcomes that judged a team's answer — the attempts the
    /// quality figures count. Correct ones match the scoring engine's
    /// CorrectCount; Passed, Skipped, Bonus, Penalty and Voided are not attempts.</summary>
    private static readonly AnswerOutcome[] Attempted =
    [
        AnswerOutcome.Correct, AnswerOutcome.PassedCorrect, AnswerOutcome.Incorrect, AnswerOutcome.PassedIncorrect,
        AnswerOutcome.NoAnswer, AnswerOutcome.TimedOut,
    ];

    private static bool IsCorrect(AnswerOutcome outcome) => outcome is AnswerOutcome.Correct or AnswerOutcome.PassedCorrect;

    private readonly IAppDbContext _db;
    private readonly Standings _standings;

    public ReportingService(IAppDbContext db, Standings standings)
    {
        _db = db;
        _standings = standings;
    }

    /// <summary>P13-01: the match, each team's line from TeamMatchScore, every
    /// question that was served with the answers recorded against it
    /// (reversed ones included and marked), and the full MatchEvent timeline.</summary>
    public async Task<MatchReportDto> MatchAsync(Guid programId, Guid matchId, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, programId, matchId, cancellationToken);
        var stageName = await _db.Stages.IgnoreQueryFilters()
            .Where(s => s.Id == match.StageId)
            .Select(s => s.Name)
            .SingleAsync(cancellationToken);
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var scores = await _db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken);
        var names = await TeamNamesAsync(participants.Select(p => p.TeamId), cancellationToken);

        var teams = participants
            .Select(p =>
            {
                var score = scores.FirstOrDefault(s => s.MatchParticipantId == p.Id);
                return new MatchReportTeamDto(
                    p.TeamId,
                    names.GetValueOrDefault(p.TeamId, string.Empty),
                    p.SeatNumber,
                    p.Status.ToString(),
                    score?.TotalPoints ?? 0,
                    p.FinalRank ?? score?.Rank,
                    score?.CorrectCount ?? 0,
                    score?.IncorrectCount ?? 0,
                    score?.NoAnswerCount ?? 0,
                    score?.PassedCount ?? 0,
                    p.ExcludeFromStandings);
            })
            .OrderBy(t => t.Rank ?? int.MaxValue)
            .ThenBy(t => t.SeatNumber)
            .ToList();

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var served = await _db.MatchQuestions
            .Where(q => q.MatchId == match.Id && q.ServedAtUtc != null)
            .ToListAsync(cancellationToken);
        var questionIds = served.Select(q => q.QuestionId).Distinct().ToList();
        var texts = await _db.Questions.IgnoreQueryFilters()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.QuestionText, cancellationToken);
        var answers = await _db.AnswerRecords
            .Where(a => a.MatchId == match.Id)
            .OrderBy(a => a.AnsweredAtUtc)
            .ToListAsync(cancellationToken);

        var questions = served
            .Select(q =>
            {
                var segment = segments.Single(s => s.Id == q.MatchSegmentId);
                return new MatchReportQuestionDto(
                    q.Id,
                    q.QuestionId,
                    segment.FormatCode.ToString(),
                    segment.OrderIndex,
                    q.OrderIndex,
                    texts.GetValueOrDefault(q.QuestionId),
                    q.State.ToString(),
                    q.ServedAtUtc,
                    answers
                        .Where(a => a.MatchQuestionId == q.Id)
                        .Select(a => new MatchReportAnswerDto(
                            a.TeamId, names.GetValueOrDefault(a.TeamId, string.Empty), a.Outcome.ToString(),
                            a.IsCorrect ?? (Attempted.Contains(a.Outcome) ? IsCorrect(a.Outcome) : null),
                            a.PassNumber, a.IsReversed, a.AnsweredAtUtc))
                        .ToList());
            })
            .OrderBy(q => q.SegmentOrder)
            .ThenBy(q => q.OrderIndex)
            .ToList();

        var timeline = await _db.MatchEvents
            .Where(e => e.MatchId == match.Id)
            .OrderBy(e => e.SequenceNumber)
            .Select(e => new MatchEventItemDto(e.SequenceNumber, e.EventType, e.PayloadJson, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new MatchReportDto(
            match.Id,
            MatchSetup.DisplayName(match),
            match.MatchNumber,
            match.MatchKind.ToString(),
            match.StageId,
            stageName,
            match.State.ToString(),
            match.StartedAtUtc,
            match.CompletedAtUtc,
            match.WinnerTeamId,
            match.WinnerTeamId is { } winner ? names.GetValueOrDefault(winner) : null,
            match.IsTied,
            match.AbandonReason,
            teams,
            questions,
            timeline);
    }

    /// <summary>P13-02: the stage's matches, its standings (with the
    /// criteria notes for level teams), every qualification row — each
    /// carrying the tie-break that put it there, where one did — and every
    /// tie-break the stage recorded, abandoned ones included.</summary>
    public async Task<StageSummaryReportDto> StageAsync(Guid programId, Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages.SingleOrDefaultAsync(s => s.Id == stageId && s.ProgramId == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{stageId}' was not found.");

        var matches = await _db.Matches
            .Where(m => m.StageId == stageId)
            .OrderBy(m => m.MatchKind)
            .ThenBy(m => m.MatchNumber)
            .ToListAsync(cancellationToken);
        var qualifications = await _db.StageQualifications
            .Where(q => q.FromStageId == stageId)
            .OrderBy(q => q.StageRank)
            .ToListAsync(cancellationToken);
        var ties = await _db.TieBreakEvents
            .Where(t => t.StageId == stageId)
            .OrderBy(t => t.DetectedAtUtc)
            .ToListAsync(cancellationToken);
        var tieIds = ties.Select(t => t.Id).ToList();
        var tieParticipants = await _db.TieBreakParticipants
            .Where(p => tieIds.Contains(p.TieBreakEventId))
            .ToListAsync(cancellationToken);

        var names = await TeamNamesAsync(
            matches.Where(m => m.WinnerTeamId != null).Select(m => m.WinnerTeamId!.Value)
                .Concat(qualifications.Select(q => q.TeamId))
                .Concat(tieParticipants.Select(p => p.TeamId)),
            cancellationToken);

        var matchViews = matches
            .Select(m => new StageReportMatchDto(
                m.Id, MatchSetup.DisplayName(m), m.MatchKind.ToString(), m.State.ToString(), m.WinnerTeamId,
                m.WinnerTeamId is { } winner ? names.GetValueOrDefault(winner) : null, m.IsTied))
            .ToList();

        var qualifiers = qualifications
            .Select(q => new StageReportQualifierDto(
                q.TeamId,
                names.GetValueOrDefault(q.TeamId, string.Empty),
                q.QualificationReason.ToString(),
                q.StageScore,
                q.StageRank,
                q.IsCommitted,
                q.ToStageId,
                q.SourceMatchId,
                q.SourceMatchId is { } sourceId ? matches.FirstOrDefault(m => m.Id == sourceId) is { } source ? MatchSetup.DisplayName(source) : null : null,
                q.TieBreakEventId,
                q.TieBreakEventId is { } tieId && ties.FirstOrDefault(t => t.Id == tieId) is { } tie ? Provenance(tie, matches) : null))
            .ToList();

        var tieViews = ties
            .Select(t => new StageReportTieBreakDto(
                t.Id,
                t.Scope.ToString(),
                t.State.ToString(),
                t.ContestedRank,
                t.ContestedSlots,
                t.ResolutionMethod?.ToString(),
                t.ResolvedByCriterion,
                t.TieBreakMatchId,
                t.RoundsPlayed,
                t.DetectedAtUtc,
                t.ResolvedAtUtc,
                t.Notes,
                tieParticipants
                    .Where(p => p.TieBreakEventId == t.Id)
                    .OrderBy(p => p.ResultRank ?? int.MaxValue)
                    .ThenByDescending(p => p.EnteringScore)
                    .Select(p => new StageReportTieParticipantDto(
                        p.TeamId, names.GetValueOrDefault(p.TeamId, string.Empty), p.EnteringScore, p.TieBreakPoints, p.ResultRank, p.Qualified))
                    .ToList()))
            .ToList();

        return new StageSummaryReportDto(
            stage.Id,
            stage.Name,
            stage.StageType.ToString(),
            stage.State.ToString(),
            stage.OrderIndex,
            stage.StartedAtUtc,
            stage.CompletedAtUtc,
            matchViews,
            await _standings.ForStageAsync(programId, stageId, cancellationToken),
            qualifiers,
            tieViews);
    }

    /// <summary>P13-03: every match the team has played (anything past
    /// setup), its totals over the completed ones, accuracy, a per-format
    /// breakdown from its answers and score events, and its qualification
    /// history. Totals come from TeamMatchScore, never a recount.</summary>
    public async Task<TeamPerformanceReportDto> TeamAsync(Guid programId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await _db.Teams.SingleOrDefaultAsync(t => t.Id == teamId && t.ProgramId == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{teamId}' was not found.");

        var played = await (
                from participant in _db.MatchParticipants
                where participant.TeamId == teamId
                join match in _db.Matches on participant.MatchId equals match.Id
                where match.ProgramId == programId && match.State != MatchState.Draft && match.State != MatchState.Ready
                select new { Participant = participant, Match = match })
            .ToListAsync(cancellationToken);
        var matchIds = played.Select(p => p.Match.Id).ToList();
        var participantIds = played.Select(p => p.Participant.Id).ToList();
        var scores = await _db.TeamMatchScores.Where(s => participantIds.Contains(s.MatchParticipantId)).ToListAsync(cancellationToken);
        var qualifications = await _db.StageQualifications.Where(q => q.TeamId == teamId).ToListAsync(cancellationToken);
        var stageIds = played.Select(p => p.Match.StageId).Concat(qualifications.Select(q => q.FromStageId)).Distinct().ToList();
        var stageNames = await _db.Stages.IgnoreQueryFilters()
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var matches = played
            .OrderBy(p => p.Match.StartedAtUtc)
            .Select(p =>
            {
                var score = scores.FirstOrDefault(s => s.MatchParticipantId == p.Participant.Id);
                return new TeamPerformanceMatchDto(
                    p.Match.Id,
                    MatchSetup.DisplayName(p.Match),
                    p.Match.StageId,
                    stageNames.GetValueOrDefault(p.Match.StageId, string.Empty),
                    p.Match.MatchKind.ToString(),
                    p.Match.State.ToString(),
                    score?.TotalPoints ?? 0,
                    p.Participant.FinalRank ?? score?.Rank,
                    p.Match.WinnerTeamId == teamId,
                    score?.CorrectCount ?? 0,
                    score?.IncorrectCount ?? 0);
            })
            .ToList();

        var completed = played.Where(p => p.Match.State == MatchState.Completed).Select(p => p.Participant.Id).ToHashSet();
        var counted = scores.Where(s => completed.Contains(s.MatchParticipantId)).ToList();
        var correct = counted.Sum(s => s.CorrectCount);
        var incorrect = counted.Sum(s => s.IncorrectCount);
        var total = counted.Sum(s => s.TotalPoints);

        var segmentFormats = await _db.MatchSegments
            .Where(s => matchIds.Contains(s.MatchId))
            .ToDictionaryAsync(s => s.Id, s => s.FormatCode.ToString(), cancellationToken);
        var answers = await _db.AnswerRecords
            .Where(a => a.TeamId == teamId && matchIds.Contains(a.MatchId) && !a.IsReversed && Attempted.Contains(a.Outcome))
            .Select(a => new { a.MatchSegmentId, a.Outcome })
            .ToListAsync(cancellationToken);
        var points = await _db.ScoreEvents
            .Where(e => e.TeamId == teamId && matchIds.Contains(e.MatchId) && e.MatchSegmentId != null)
            .Select(e => new { e.MatchSegmentId, e.Points })
            .ToListAsync(cancellationToken);
        var formats = segmentFormats.Values.Distinct()
            .Select(format => new TeamFormatPerformanceDto(
                format,
                answers.Count(a => segmentFormats.GetValueOrDefault(a.MatchSegmentId) == format),
                answers.Count(a => IsCorrect(a.Outcome) && segmentFormats.GetValueOrDefault(a.MatchSegmentId) == format),
                points.Where(e => segmentFormats.GetValueOrDefault(e.MatchSegmentId!.Value) == format).Sum(e => e.Points)))
            .Where(f => f.Attempts > 0 || f.Points != 0)
            .OrderBy(f => f.Format)
            .ToList();

        var ties = await TiesByIdAsync(qualifications.Where(q => q.TieBreakEventId != null).Select(q => q.TieBreakEventId!.Value), cancellationToken);
        var tieMatchIds = ties.Values.Where(t => t.TieBreakMatchId != null).Select(t => t.TieBreakMatchId!.Value).ToList();
        var tieMatches = await _db.Matches.IgnoreQueryFilters().Where(m => tieMatchIds.Contains(m.Id)).ToListAsync(cancellationToken);
        var history = qualifications
            .Select(q => new TeamQualificationDto(
                q.FromStageId,
                stageNames.GetValueOrDefault(q.FromStageId, string.Empty),
                q.QualificationReason.ToString(),
                q.StageScore,
                q.StageRank,
                q.IsCommitted,
                q.TieBreakEventId is { } tieId && ties.TryGetValue(tieId, out var tie) ? Provenance(tie, tieMatches) : null))
            .ToList();

        var overall = (await _standings.OverallAsync(programId, cancellationToken)).SingleOrDefault(s => s.TeamId == teamId);

        return new TeamPerformanceReportDto(
            team.Id,
            team.DisplayName,
            team.Status.ToString(),
            completed.Count,
            total,
            completed.Count == 0 ? 0 : Math.Round((double)total / completed.Count, 2),
            played.Count(p => p.Match.State == MatchState.Completed && p.Match.WinnerTeamId == teamId),
            correct,
            incorrect,
            counted.Sum(s => s.NoAnswerCount),
            counted.Sum(s => s.PassedCount),
            correct + incorrect == 0 ? 0 : Math.Round((double)correct / (correct + incorrect), 4),
            overall?.Rank ?? 0,
            matches,
            formats,
            history);
    }

    /// <summary>P13-04: every question served in the program (or one stage),
    /// how often, and how it was answered. A question is flagged
    /// <c>EveryoneCorrect</c> when every attempt at it was correct —
    /// the "too easy" signal — and <c>NobodyCorrect</c> when none was, which
    /// often means a wrong answer key. Reversed and voided answers do not count.</summary>
    public async Task<QuestionUsageReportDto> QuestionUsageAsync(
        Guid programId, Guid? stageId, bool flaggedOnly, CancellationToken cancellationToken)
    {
        if (stageId is { } id && !await _db.Stages.AnyAsync(s => s.Id == id && s.ProgramId == programId, cancellationToken))
        {
            throw new KeyNotFoundException($"Stage '{id}' was not found.");
        }

        var usage = await _db.QuestionUsageHistories
            .Where(u => u.ProgramId == programId && (stageId == null || u.StageId == stageId))
            .Select(u => new { u.QuestionId, u.MatchId, u.UsedAtUtc })
            .ToListAsync(cancellationToken);
        var questionIds = usage.Select(u => u.QuestionId).Distinct().ToList();
        var questions = await _db.Questions.IgnoreQueryFilters()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);
        var answers = await (
                from answer in _db.AnswerRecords
                where answer.ProgramId == programId && !answer.IsReversed && Attempted.Contains(answer.Outcome)
                join served in _db.MatchQuestions on answer.MatchQuestionId equals served.Id
                join match in _db.Matches on answer.MatchId equals match.Id
                where stageId == null || match.StageId == stageId
                select new { served.QuestionId, answer.Outcome })
            .ToListAsync(cancellationToken);

        var entries = usage
            .GroupBy(u => u.QuestionId)
            .Where(g => questions.ContainsKey(g.Key))
            .Select(g =>
            {
                var question = questions[g.Key];
                var judged = answers.Where(a => a.QuestionId == g.Key).ToList();
                var right = judged.Count(a => IsCorrect(a.Outcome));
                var everyone = judged.Count > 0 && right == judged.Count;
                var nobody = judged.Count > 0 && right == 0;
                var flags = new List<string>();
                if (everyone)
                {
                    flags.Add("EveryoneCorrect");
                }

                if (nobody)
                {
                    flags.Add("NobodyCorrect");
                }

                if (judged.Count == 0)
                {
                    flags.Add("NeverAnswered");
                }

                return new QuestionUsageEntryDto(
                    g.Key,
                    question.QuestionText,
                    question.FormatCode.ToString(),
                    question.DifficultyLevel.ToString(),
                    g.Count(),
                    g.Select(u => u.MatchId).Distinct().Count(),
                    judged.Count,
                    right,
                    judged.Count - right,
                    judged.Count == 0 ? 0 : Math.Round((double)right / judged.Count, 4),
                    everyone,
                    nobody,
                    flags,
                    g.Max(u => u.UsedAtUtc));
            })
            .OrderByDescending(e => e.TimesUsed)
            .ThenBy(e => e.QuestionText)
            .ToList();

        var flagged = entries.Count(e => e.Flags.Count > 0);
        return new QuestionUsageReportDto(
            entries.Count,
            flagged,
            flaggedOnly ? entries.Where(e => e.Flags.Count > 0).ToList() : entries);
    }

    private Task<Dictionary<Guid, TieBreakEvent>> TiesByIdAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return _db.TieBreakEvents.IgnoreQueryFilters().Where(t => list.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
    }

    /// <summary>How a tie-break decided a qualifier, in words — the
    /// "tie-break provenance" P13-02 requires.</summary>
    internal static string Provenance(TieBreakEvent tie, IReadOnlyList<Domain.Tournament.Match> stageMatches)
    {
        var how = tie.ResolutionMethod switch
        {
            null => $"still {tie.State}",
            TieBreakResolutionMethod.Criteria when tie.ResolvedByCriterion is { } criterion => $"criteria ({criterion})",
            TieBreakResolutionMethod.PlayedTieBreak when tie.TieBreakMatchId is { } matchId
                && stageMatches.FirstOrDefault(m => m.Id == matchId) is { } played => $"tie-break match '{MatchSetup.DisplayName(played)}'",
            { } method => method.ToString(),
        };

        return $"Tie for rank {tie.ContestedRank} ({tie.ContestedSlots} slot(s), {tie.Scope}) resolved by {how}"
            + (tie.Notes is { Length: > 0 } notes ? $" — {notes}" : string.Empty);
    }

    private Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken)
    {
        var ids = teamIds.Distinct().ToList();
        return _db.Teams.IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);
    }
}
