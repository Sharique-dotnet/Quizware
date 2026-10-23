using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification.Dtos;
using Quizware.Application.Scoring;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Qualification;

/// <summary>A tie found while working out who qualifies. <see cref="ContestedSlots"/>
/// places are at stake among <see cref="TeamIds"/> (all level on <see cref="Points"/>);
/// a tie wholly below the cut contests none and is only recorded.</summary>
public sealed record DetectedTie(
    TieBreakScope Scope, Guid? SourceMatchId, int ContestedRank, int ContestedSlots, IReadOnlyList<Guid> TeamIds, int Points)
{
    public bool AffectsQualification => ContestedSlots > 0 && ContestedSlots < TeamIds.Count;
}

/// <summary>Where a tie stands: the teams it has already placed (in or out),
/// the ones still contested, and the record behind it, if one was saved.</summary>
public sealed record TieAssessment(
    DetectedTie Tie,
    TieBreakEvent? Event,
    TieBreakOrdering Criteria,
    bool Resolved,
    IReadOnlyList<Guid> Qualified,
    IReadOnlyList<Guid> Contested,
    IReadOnlyList<Guid> Out,
    QualificationReason Reason);

public sealed record QualifierEntry(
    Guid TeamId, Guid? SourceMatchId, QualificationReason Reason, Guid? TieBreakEventId);

/// <summary>Everything the preview, the tie endpoints and the commit need.</summary>
public sealed record QualificationEvaluation(
    Stage FromStage,
    Stage? ToStage,
    QualificationRule Rule,
    TieBreakRule? TieBreakRule,
    int RegularMatchCount,
    bool AllMatchesComplete,
    bool AlreadyCommitted,
    IReadOnlyList<QualifierEntry> Qualifiers,
    IReadOnlyList<Guid> Eliminated,
    IReadOnlyList<TieAssessment> Ties,
    IReadOnlyDictionary<Guid, Domain.Tournament.Match> MatchOfTeam,
    IReadOnlyDictionary<Guid, Scoring.Dtos.StandingEntryDto> Standing,
    IReadOnlyList<string> Blockers)
{
    public bool CanCommit => Blockers.Count == 0;

    public IEnumerable<TieAssessment> UnresolvedBlockingTies => Ties.Where(t => t.Tie.AffectsQualification && !t.Resolved);
}

/// <summary>P11-01/02/03: works out who advances from a stage (W5 in
/// 05-API-Design.md). Winners are the top <c>WinnersPerMatch</c> active
/// participants of each completed regular match, by match points; the rest
/// are ranked by stage points and the best <c>BestRemainingAcrossStage</c>
/// advance; manual wildcards fill the remaining slots. A tie that straddles
/// one of those cuts is a <see cref="DetectedTie"/>: the stage's ordered
/// criteria are tried first, and only a tie they cannot settle needs a
/// tie-break match or a manual decision. A tie wholly below the stage cut is
/// recorded but never blocks. Reading never writes; a
/// <see cref="ITieRecorder"/> (detect and commit) saves what was found.</summary>
public sealed class QualificationEvaluator
{
    private readonly IAppDbContext _db;
    private readonly Standings _standings;
    private readonly ITieBreakCriteriaService _criteria;

    public QualificationEvaluator(IAppDbContext db, Standings standings, ITieBreakCriteriaService criteria)
    {
        _db = db;
        _standings = standings;
        _criteria = criteria;
    }

    public Task<QualificationEvaluation> EvaluateAsync(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        EvaluateAsync(programId, stageId, recorder: null, cancellationToken);

    public async Task<QualificationEvaluation> EvaluateAsync(
        Guid programId, Guid stageId, ITieRecorder? recorder, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages.SingleOrDefaultAsync(s => s.Id == stageId && s.ProgramId == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{stageId}' was not found.");
        var rule = await _db.QualificationRules.SingleOrDefaultAsync(r => r.ProgramId == programId && r.FromStageId == stageId, cancellationToken)
            ?? throw new InvalidStateTransitionException(
                $"Stage '{stage.Name}' has no qualification rule. Add one under rules/qualification before previewing who advances.");
        var toStage = rule.ToStageId is { } toStageId
            ? await _db.Stages.SingleOrDefaultAsync(s => s.Id == toStageId, cancellationToken)
            : await _db.Stages
                .Where(s => s.ProgramId == programId && s.OrderIndex > stage.OrderIndex)
                .OrderBy(s => s.OrderIndex)
                .FirstOrDefaultAsync(cancellationToken);

        var matches = await _db.Matches
            .Where(m => m.StageId == stageId && m.MatchKind == MatchKind.Regular && m.State != MatchState.Abandoned)
            .OrderBy(m => m.MatchNumber)
            .ToListAsync(cancellationToken);
        var completed = matches.Where(m => m.State == MatchState.Completed).ToList();
        var completedIds = completed.Select(m => m.Id).ToList();

        var standing = (await _standings.ForStageAsync(programId, stageId, cancellationToken)).Standings.ToDictionary(s => s.TeamId);
        var participants = (await _db.MatchParticipants
                .Where(p => completedIds.Contains(p.MatchId) && p.Status == ParticipantStatus.Active && !p.ExcludeFromStandings)
                .ToListAsync(cancellationToken))
            .Where(p => standing.ContainsKey(p.TeamId))
            .ToList();
        var matchOfTeam = participants
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => completed.First(m => m.Id == g.First().MatchId));

        var tieBreakRule = await ResolveTieBreakRuleAsync(programId, stageId, TieBreakScope.StageQualification, cancellationToken);
        var events = await _db.TieBreakEvents
            .Where(e => e.StageId == stageId && e.State != TieBreakEventState.Abandoned)
            .ToListAsync(cancellationToken);
        var eventIds = events.Select(e => e.Id).ToList();
        var eventTeams = (await _db.TieBreakParticipants.Where(p => eventIds.Contains(p.TieBreakEventId)).ToListAsync(cancellationToken))
            .ToLookup(p => p.TieBreakEventId);
        var context = new TieContext(stageId, events, eventTeams, recorder);

        var qualifiers = new List<QualifierEntry>();
        var ties = new List<TieAssessment>();
        var pending = new HashSet<Guid>();
        string NameOf(Guid teamId) => standing.GetValueOrDefault(teamId)?.TeamName ?? string.Empty;

        // 1. Match winners.
        foreach (var match in completed)
        {
            var ordered = participants
                .Where(p => p.MatchId == match.Id)
                .OrderByDescending(p => p.FinalScore)
                .ThenBy(p => NameOf(p.TeamId))
                .Select(p => new Candidate(p.TeamId, p.FinalScore))
                .ToList();
            var cut = Cut(ordered, rule.WinnersPerMatch);
            qualifiers.AddRange(cut.Above.Select(c => new QualifierEntry(c.TeamId, match.Id, QualificationReason.MatchWinner, null)));
            if (cut.Tie is { } tie)
            {
                var detected = new DetectedTie(
                    TieBreakScope.MatchRanking, match.Id, cut.Above.Count + 1, tie.Slots, tie.Teams.Select(t => t.TeamId).ToList(), tie.Teams[0].Points);
                var assessment = await AssessAsync(detected, context, cancellationToken);
                ties.Add(assessment);
                qualifiers.AddRange(assessment.Qualified.Select(id => new QualifierEntry(id, match.Id, assessment.Reason, assessment.Event?.Id)));
                pending.UnionWith(assessment.Contested);
            }
        }

        // 2. Best remaining across the stage.
        var placed = qualifiers.Select(q => q.TeamId).ToHashSet();
        var pool = participants
            .Select(p => p.TeamId)
            .Distinct()
            .Where(id => !placed.Contains(id) && !pending.Contains(id))
            .Where(id => rule.MinScoreThreshold is not { } minimum || standing[id].Score >= minimum)
            .OrderByDescending(id => standing[id].Score)
            .ThenBy(id => standing[id].Rank)
            .Select(id => new Candidate(id, standing[id].Score))
            .ToList();
        var stageCut = Cut(pool, rule.BestRemainingAcrossStage);
        qualifiers.AddRange(stageCut.Above.Select(c => new QualifierEntry(c.TeamId, matchOfTeam[c.TeamId].Id, QualificationReason.BestRemaining, null)));
        if (stageCut.Tie is { } boundary)
        {
            var detected = new DetectedTie(
                TieBreakScope.StageQualification, null, qualifiers.Count + 1, boundary.Slots,
                boundary.Teams.Select(t => t.TeamId).ToList(), boundary.Teams[0].Points);
            var assessment = await AssessAsync(detected, context, cancellationToken);
            ties.Add(assessment);
            qualifiers.AddRange(assessment.Qualified.Select(id => new QualifierEntry(id, matchOfTeam[id].Id, assessment.Reason, assessment.Event?.Id)));
            pending.UnionWith(assessment.Contested);
        }

        // A tie wholly below the cut is recorded, never blocking.
        var belowRank = qualifiers.Count + pending.Count + 1;
        foreach (var level in stageCut.Below.GroupBy(c => c.Points).Where(g => g.Count() > 1))
        {
            var detected = new DetectedTie(
                TieBreakScope.StageQualification, null, belowRank + stageCut.Below.Count(c => c.Points > level.Key), 0,
                level.Select(c => c.TeamId).ToList(), level.Key);
            ties.Add(await AssessAsync(detected, context, cancellationToken));
        }

        // 3. Manual wildcards, from teams that did not otherwise qualify.
        placed = qualifiers.Select(q => q.TeamId).ToHashSet();
        var wildcards = await _db.StageQualifications
            .Where(q => q.FromStageId == stageId && q.QualificationReason == QualificationReason.Manual)
            .OrderBy(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        foreach (var wildcard in wildcards
                     .Where(w => matchOfTeam.ContainsKey(w.TeamId) && !placed.Contains(w.TeamId) && !pending.Contains(w.TeamId))
                     .Take(rule.ManualWildcardSlots))
        {
            qualifiers.Add(new QualifierEntry(wildcard.TeamId, matchOfTeam[wildcard.TeamId].Id, QualificationReason.Manual, null));
        }

        placed = qualifiers.Select(q => q.TeamId).ToHashSet();
        var eliminated = matchOfTeam.Keys
            .Where(id => !placed.Contains(id) && !pending.Contains(id))
            .OrderBy(id => standing[id].Rank)
            .ToList();

        if (recorder is not null)
        {
            await recorder.CloseStaleAsync(context.Unmatched(), cancellationToken);
        }

        var alreadyCommitted = await _db.StageQualifications.AnyAsync(q => q.FromStageId == stageId && q.IsCommitted, cancellationToken);

        var blockers = new List<string>();
        if (alreadyCommitted)
        {
            blockers.Add("Qualification from this stage is already committed.");
        }

        if (matches.Count == 0)
        {
            blockers.Add("The stage has no matches.");
        }
        else if (completed.Count < matches.Count)
        {
            blockers.Add($"{matches.Count - completed.Count} of {matches.Count} matches in the stage are not completed yet.");
        }

        if (toStage is null)
        {
            blockers.Add("There is no next stage to qualify into.");
        }

        var unresolved = ties.Count(t => t.Tie.AffectsQualification && !t.Resolved);
        if (unresolved > 0)
        {
            blockers.Add($"{unresolved} unresolved tie{(unresolved == 1 ? string.Empty : "s")} on the qualification boundary.");
        }

        return new QualificationEvaluation(
            stage,
            toStage,
            rule,
            tieBreakRule,
            matches.Count,
            matches.Count > 0 && completed.Count == matches.Count,
            alreadyCommitted,
            qualifiers.OrderBy(q => standing[q.TeamId].Rank).ToList(),
            eliminated,
            ties,
            matchOfTeam,
            standing,
            blockers);
    }

    /// <summary>See <see cref="TieBreakRuleResolution"/>.</summary>
    public async Task<TieBreakRule?> ResolveTieBreakRuleAsync(Guid programId, Guid stageId, TieBreakScope scope, CancellationToken cancellationToken)
    {
        var rules = await _db.TieBreakRules
            .Where(r => r.ProgramId == programId && (r.StageId == stageId || r.StageId == null))
            .ToListAsync(cancellationToken);
        return TieBreakRuleResolution.Resolve(rules, stageId, scope);
    }

    public static QualificationReason ReasonFor(TieBreakResolutionMethod? method) => method switch
    {
        TieBreakResolutionMethod.Criteria => QualificationReason.CriteriaTieBreak,
        TieBreakResolutionMethod.PlayedTieBreak => QualificationReason.TieBreakWin,
        _ => QualificationReason.Manual,
    };

    private async Task<TieAssessment> AssessAsync(DetectedTie tie, TieContext context, CancellationToken cancellationToken)
    {
        var criteria = await _criteria.OrderTiedTeamsAsync(context.StageId, tie.TeamIds, cancellationToken);
        var existing = context.Claim(tie);

        if (existing is not null)
        {
            var teams = context.TeamsOf(existing);
            var qualified = teams.Where(t => !t.IsContested && t.Qualified).OrderBy(t => t.ResultRank).Select(t => t.TeamId).ToList();
            var contested = teams.Where(t => t.IsContested).Select(t => t.TeamId).ToList();
            var placedOut = teams.Where(t => !t.IsContested && !t.Qualified).OrderBy(t => t.ResultRank).Select(t => t.TeamId).ToList();
            var resolved = existing.State == TieBreakEventState.Resolved;
            return new TieAssessment(
                tie, existing, criteria, resolved, qualified, resolved ? [] : contested, resolved ? placedOut.Concat(contested).ToList() : placedOut,
                ReasonFor(existing.ResolutionMethod));
        }

        var split = Split(criteria, tie.ContestedSlots);
        var assessment = new TieAssessment(
            tie, null, criteria, split.Contested.Count == 0, split.Qualified, split.Contested, split.Out, QualificationReason.CriteriaTieBreak);

        return context.Recorder is null
            ? assessment
            : assessment with { Event = await context.Recorder.RecordAsync(assessment, cancellationToken) };
    }

    /// <summary>Walks the criteria's ranked groups: whole groups that fit in
    /// the slots qualify, the group straddling the last slot stays contested,
    /// and everything after it is out.</summary>
    internal static (List<Guid> Qualified, List<Guid> Contested, List<Guid> Out) Split(TieBreakOrdering ordering, int slots)
    {
        var qualified = new List<Guid>();
        var contested = new List<Guid>();
        var placedOut = new List<Guid>();
        foreach (var group in ordering.RankedGroups)
        {
            var remaining = slots - qualified.Count;
            if (contested.Count > 0 || remaining <= 0)
            {
                placedOut.AddRange(group);
            }
            else if (group.Count <= remaining)
            {
                qualified.AddRange(group);
            }
            else
            {
                contested.AddRange(group);
            }
        }

        return (qualified, contested, placedOut);
    }

    private sealed record Candidate(Guid TeamId, int Points);

    private sealed record CutResult(List<Candidate> Above, (List<Candidate> Teams, int Slots)? Tie, List<Candidate> Below);

    /// <summary>The best <paramref name="slots"/> of a list ordered by points,
    /// best first, and — when the teams either side of the last slot are
    /// level — the whole level group with the slots it is contesting.</summary>
    private static CutResult Cut(IReadOnlyList<Candidate> ordered, int slots)
    {
        if (slots <= 0)
        {
            return new CutResult([], null, ordered.ToList());
        }

        if (ordered.Count <= slots)
        {
            return new CutResult(ordered.ToList(), null, []);
        }

        var boundary = ordered[slots - 1].Points;
        if (ordered[slots].Points != boundary)
        {
            return new CutResult(ordered.Take(slots).ToList(), null, ordered.Skip(slots).ToList());
        }

        var above = ordered.Where(c => c.Points > boundary).ToList();
        return new CutResult(
            above,
            (ordered.Where(c => c.Points == boundary).ToList(), slots - above.Count),
            ordered.Where(c => c.Points < boundary).ToList());
    }

    /// <summary>The saved tie records of one evaluation, each matched to at
    /// most one tie found this time; what is left unmatched has gone stale.</summary>
    private sealed class TieContext
    {
        private readonly List<TieBreakEvent> _unclaimed;
        private readonly ILookup<Guid, TieBreakParticipant> _teams;

        public TieContext(Guid stageId, List<TieBreakEvent> events, ILookup<Guid, TieBreakParticipant> teams, ITieRecorder? recorder)
        {
            StageId = stageId;
            _unclaimed = events.ToList();
            _teams = teams;
            Recorder = recorder;
        }

        public Guid StageId { get; }

        public ITieRecorder? Recorder { get; }

        public IReadOnlyList<TieBreakParticipant> TeamsOf(TieBreakEvent tieBreak) => _teams[tieBreak.Id].ToList();

        public TieBreakEvent? Claim(DetectedTie tie)
        {
            var teamIds = tie.TeamIds.OrderBy(id => id).ToList();
            var match = _unclaimed
                .Where(e => e.Scope == tie.Scope && e.SourceMatchId == tie.SourceMatchId && e.ContestedSlots == tie.ContestedSlots
                    && _teams[e.Id].Select(p => p.TeamId).OrderBy(id => id).SequenceEqual(teamIds))
                .OrderByDescending(e => e.DetectedAtUtc)
                .FirstOrDefault();
            if (match is not null)
            {
                _unclaimed.Remove(match);
            }

            return match;
        }

        public IReadOnlyList<TieBreakEvent> Unmatched() => _unclaimed;
    }
}

/// <summary>Saves what an evaluation found: a new tie record (with its
/// criteria result) for every tie not yet on file, and closes records that
/// no longer match any tie.</summary>
public interface ITieRecorder
{
    Task<TieBreakEvent?> RecordAsync(TieAssessment assessment, CancellationToken cancellationToken);

    Task CloseStaleAsync(IReadOnlyList<TieBreakEvent> stale, CancellationToken cancellationToken);
}
