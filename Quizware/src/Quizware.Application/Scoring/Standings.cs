using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification;
using Quizware.Application.Scoring.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;

namespace Quizware.Application.Scoring;

/// <summary>P10-05: standings read from the pre-aggregated TeamStageScore
/// rows — never a recount of answers. A team is left out of a stage's
/// standings when it was disqualified there with ExcludeFromStandings, and
/// out of every standing when the team itself is withdrawn or disqualified;
/// its match history stays on its own record either way.</summary>
public sealed class Standings
{
    private readonly IAppDbContext _db;
    private readonly ITieBreakCriteriaService _tieBreaks;

    public Standings(IAppDbContext db, ITieBreakCriteriaService tieBreaks)
    {
        _db = db;
        _tieBreaks = tieBreaks;
    }

    public async Task<StageStandingsDto> ForStageAsync(Guid programId, Guid stageId, CancellationToken cancellationToken)
    {
        if (!await _db.Stages.AnyAsync(s => s.Id == stageId && s.ProgramId == programId, cancellationToken))
        {
            throw new KeyNotFoundException($"Stage '{stageId}' was not found.");
        }

        var scores = await CountedStageScoresAsync(programId, [stageId], cancellationToken);
        var names = await TeamNamesAsync(scores.Select(s => s.TeamId), cancellationToken);
        var standings = new List<StandingEntryDto>();
        var notes = new List<string>();

        foreach (var level in scores.GroupBy(s => s.TotalPoints).OrderByDescending(g => g.Key))
        {
            var teamIds = level.Select(s => s.TeamId).OrderBy(id => names.GetValueOrDefault(id)).ToList();
            if (teamIds.Count == 1)
            {
                standings.Add(new StandingEntryDto(teamIds[0], names.GetValueOrDefault(teamIds[0], string.Empty), level.Key, standings.Count + 1));
                continue;
            }

            var ordering = await _tieBreaks.OrderTiedTeamsAsync(stageId, teamIds, cancellationToken);
            foreach (var group in ordering.RankedGroups)
            {
                var rank = standings.Count + 1;
                standings.AddRange(group.Select(id => new StandingEntryDto(id, names.GetValueOrDefault(id, string.Empty), level.Key, rank)));
            }

            notes.Add(DescribeTie(level.Key, ordering, names));
        }

        return new StageStandingsDto(stageId, standings, notes);
    }

    public async Task<IReadOnlyList<StandingEntryDto>> OverallAsync(Guid programId, CancellationToken cancellationToken)
    {
        var stageIds = await _db.Stages.Where(s => s.ProgramId == programId).Select(s => s.Id).ToListAsync(cancellationToken);
        var totals = (await CountedStageScoresAsync(programId, stageIds, cancellationToken))
            .GroupBy(s => s.TeamId)
            .Select(g => (TeamId: g.Key, Points: g.Sum(s => s.TotalPoints)))
            .OrderByDescending(t => t.Points)
            .ToList();
        var names = await TeamNamesAsync(totals.Select(t => t.TeamId), cancellationToken);

        var standings = new List<StandingEntryDto>(totals.Count);
        foreach (var (teamId, points) in totals.OrderByDescending(t => t.Points).ThenBy(t => names.GetValueOrDefault(t.TeamId)))
        {
            var rank = standings.Count > 0 && standings[^1].Score == points ? standings[^1].Rank : standings.Count + 1;
            standings.Add(new StandingEntryDto(teamId, names.GetValueOrDefault(teamId, string.Empty), points, rank));
        }

        return standings;
    }

    /// <summary>Every match the team has played (anything past setup), with
    /// its match score and final rank — 0 while unranked (still running,
    /// abandoned, or excluded from standings). The overall figures come from
    /// the overall standings; a team left out of them has rank 0.</summary>
    public async Task<TeamStandingDto> ForTeamAsync(Guid programId, Guid teamId, CancellationToken cancellationToken)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == teamId && t.ProgramId == programId, cancellationToken))
        {
            throw new KeyNotFoundException($"Team '{teamId}' was not found.");
        }

        var records = await (
            from participant in _db.MatchParticipants
            where participant.TeamId == teamId
            join match in _db.Matches on participant.MatchId equals match.Id
            where match.State != MatchState.Draft && match.State != MatchState.Ready
            join score in _db.TeamMatchScores on participant.Id equals score.MatchParticipantId into scores
            from score in scores.DefaultIfEmpty()
            orderby match.StartedAtUtc
            select new TeamMatchRecordDto(
                match.Id,
                match.Name ?? "Match " + match.MatchNumber,
                score != null ? score.TotalPoints : 0,
                participant.FinalRank ?? 0))
            .ToListAsync(cancellationToken);

        var overall = await OverallAsync(programId, cancellationToken);
        var mine = overall.SingleOrDefault(s => s.TeamId == teamId);
        return new TeamStandingDto(teamId, records, mine?.Score ?? 0, mine?.Rank ?? 0);
    }

    private async Task<List<TeamStageScore>> CountedStageScoresAsync(
        Guid programId, IReadOnlyList<Guid> stageIds, CancellationToken cancellationToken)
    {
        var scores = await _db.TeamStageScores
            .Where(s => s.ProgramId == programId && stageIds.Contains(s.StageId))
            .ToListAsync(cancellationToken);

        var excludedInStage = await (
            from participant in _db.MatchParticipants
            where participant.ExcludeFromStandings
            join match in _db.Matches on participant.MatchId equals match.Id
            where stageIds.Contains(match.StageId)
            select new { match.StageId, participant.TeamId })
            .ToListAsync(cancellationToken);
        var outOfTheProgram = await _db.Teams
            .Where(t => t.ProgramId == programId && (t.Status == TeamStatus.Withdrawn || t.Status == TeamStatus.Disqualified))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        return scores
            .Where(s => !outOfTheProgram.Contains(s.TeamId)
                && !excludedInStage.Any(e => e.StageId == s.StageId && e.TeamId == s.TeamId))
            .ToList();
    }

    private Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken)
    {
        var ids = teamIds.Distinct().ToList();
        return _db.Teams.IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);
    }

    private static string DescribeTie(int points, TieBreakOrdering ordering, IReadOnlyDictionary<Guid, string> names)
    {
        string Names(IEnumerable<Guid> ids) => string.Join(", ", ids.Select(id => names.GetValueOrDefault(id, id.ToString())));

        var order = string.Join(" > ", ordering.RankedGroups.Select(g => g.Count == 1 ? Names(g) : $"({Names(g)})"));
        var reasons = ordering.Trace.Where(t => t.Note is not null).Select(t => $"{t.Criterion}: {t.Note}").ToList();
        var outcome = ordering.IsFullyResolved
            ? $"first place decided by {ordering.DecidingCriterion}"
            : ordering.DecidingCriterion is null
                ? "still level after every criterion"
                : $"first place decided by {ordering.DecidingCriterion}; some teams still level";

        return $"Level on {points} points: {order} — {outcome}." + (reasons.Count > 0 ? $" ({string.Join("; ", reasons)})" : string.Empty);
    }
}
