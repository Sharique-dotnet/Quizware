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
}

public sealed class ReportingService : IReportingService
{
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
                            a.TeamId, names.GetValueOrDefault(a.TeamId, string.Empty), a.Outcome.ToString(), a.IsCorrect,
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
