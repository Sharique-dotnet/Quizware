using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Qualification;

/// <summary>P11-02/03: saves each newly found tie as a <see cref="TieBreakEvent"/>
/// with one <see cref="TieBreakParticipant"/> per tied team. Teams the
/// criteria placed get their rank (and whether they qualified); teams still
/// level are left contested. A tie the criteria settled is resolved on the
/// spot with the deciding criterion; one they could not settle waits for a
/// tie-break match when the rule plays one. Records that no longer match any
/// tie are abandoned unless a tie-break match is already being played.</summary>
public sealed class TieRecorder : ITieRecorder
{
    private readonly IAppDbContext _db;
    private readonly Guid _programId;
    private readonly Guid _stageId;
    private readonly TieBreakRule? _stageRule;
    private readonly TieBreakRule? _matchRule;
    private readonly Guid _userId;
    private readonly string _actor;

    public TieRecorder(
        IAppDbContext db, Guid programId, Guid stageId, TieBreakRule? stageRule, TieBreakRule? matchRule, Guid userId, string actor)
    {
        _db = db;
        _programId = programId;
        _stageId = stageId;
        _stageRule = stageRule;
        _matchRule = matchRule;
        _userId = userId;
        _actor = actor;
    }

    public static async Task<TieRecorder> CreateAsync(
        IAppDbContext db, QualificationEvaluator evaluator, Guid programId, Guid stageId, Guid userId, string actor, CancellationToken cancellationToken) =>
        new(db, programId, stageId,
            await evaluator.ResolveTieBreakRuleAsync(programId, stageId, TieBreakScope.StageQualification, cancellationToken),
            await evaluator.ResolveTieBreakRuleAsync(programId, stageId, TieBreakScope.MatchRanking, cancellationToken),
            userId, actor);

    public Task<TieBreakEvent?> RecordAsync(TieAssessment assessment, CancellationToken cancellationToken)
    {
        var tie = assessment.Tie;
        var rule = tie.Scope == TieBreakScope.MatchRanking ? _matchRule : _stageRule;
        if (rule is null)
        {
            if (!tie.AffectsQualification)
            {
                return Task.FromResult<TieBreakEvent?>(null);
            }

            throw new InvalidStateTransitionException(
                "A tie affects a qualifying place, but no tie-break rule applies to this stage. "
                + "Add one under rules/tie-break (reset-defaults seeds the recommended rules) and detect again.");
        }

        var record = TieBreakEvent.Detect(
            _programId, _stageId, rule.Id, tie.Scope, tie.ContestedRank, tie.ContestedSlots, _actor, tie.SourceMatchId);
        _db.TieBreakEvents.Add(record);

        var rankOf = new Dictionary<Guid, int>();
        var position = 1;
        foreach (var group in assessment.Criteria.RankedGroups)
        {
            foreach (var teamId in group)
            {
                rankOf[teamId] = position;
            }

            position += group.Count;
        }

        foreach (var teamId in tie.TeamIds)
        {
            var participant = TieBreakParticipant.Create(record.Id, teamId, tie.Points, _actor);
            if (!assessment.Contested.Contains(teamId) && (tie.AffectsQualification || assessment.Criteria.IsFullyResolved))
            {
                participant.RecordResult(0, rankOf.GetValueOrDefault(teamId, position), assessment.Qualified.Contains(teamId));
            }

            _db.TieBreakParticipants.Add(participant);
        }

        var settled = tie.AffectsQualification ? assessment.Resolved : assessment.Criteria.IsFullyResolved;
        if (settled)
        {
            record.Resolve(TieBreakResolutionMethod.Criteria, _userId, DecidingCriterion(assessment.Criteria));
        }
        else if (tie.AffectsQualification && rule.PlayTieBreakSegment)
        {
            record.AwaitPlay();
        }

        return Task.FromResult<TieBreakEvent?>(record);
    }

    public Task CloseStaleAsync(IReadOnlyList<TieBreakEvent> stale, CancellationToken cancellationToken)
    {
        foreach (var record in stale.Where(e => e.State is TieBreakEventState.Detected or TieBreakEventState.AwaitingPlay))
        {
            record.Abandon("No longer matches a tie — superseded by a later detection.");
        }

        return Task.CompletedTask;
    }

    private static string? DecidingCriterion(TieBreakOrdering ordering) =>
        ordering.DecidingCriterion ?? ordering.Trace.FirstOrDefault(t => t.Separated)?.Criterion;
}
