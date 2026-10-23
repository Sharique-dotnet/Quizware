using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Qualification.Commands;

public sealed record CommitQualificationCommand(Guid ProgramId, Guid StageId) : IRequest<int>;

/// <summary>P11-07 (W5 step 4): detects ties once more in the same
/// transaction, then refuses with <c>UNRESOLVED_TIE</c> while any tie on a
/// qualifying place is open. Otherwise every qualifier gets a committed
/// <see cref="StageQualification"/> row — reason, stage score and rank, and
/// the tie that decided the place, if one did — and the next stage's matches
/// are created from them per the rule's <c>SeedingMode</c>, ranked by stage
/// rank. With Manual seeding no matches are created (auto-seed picks the
/// committed teams up later). The next stage becomes Ready if it was Draft.
/// Returns the number of matches created.</summary>
public sealed class CommitQualificationCommandHandler : IRequestHandler<CommitQualificationCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly QualificationEvaluator _evaluator;
    private readonly StageSeeder _seeder;

    public CommitQualificationCommandHandler(IAppDbContext db, ICurrentUser currentUser, QualificationEvaluator evaluator, StageSeeder seeder)
    {
        _db = db;
        _currentUser = currentUser;
        _evaluator = evaluator;
        _seeder = seeder;
    }

    public async Task<int> Handle(CommitQualificationCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var evaluation = await DetectTiesCommandHandler.DetectAsync(
            _db, _currentUser, _evaluator, request.ProgramId, request.StageId, cancellationToken);

        if (evaluation.AlreadyCommitted)
        {
            throw new InvalidStateTransitionException("Qualification from this stage is already committed; roll it back first to change it.");
        }

        var unresolved = evaluation.UnresolvedBlockingTies.ToList();
        if (unresolved.Count > 0)
        {
            throw new UnresolvedTieException(
                $"{unresolved.Count} tie(s) on a qualifying place are still open (ranks {string.Join(", ", unresolved.Select(t => t.Tie.ContestedRank))}). "
                + "Play the tie-break or resolve it manually, then commit.");
        }

        var toStage = evaluation.ToStage
            ?? throw new InvalidStateTransitionException($"Stage '{evaluation.FromStage.Name}' has no next stage to qualify into.");

        var picks = await _db.StageQualifications
            .Where(q => q.FromStageId == request.StageId && q.QualificationReason == QualificationReason.Manual && !q.IsCommitted)
            .ToListAsync(cancellationToken);
        foreach (var qualifier in evaluation.Qualifiers)
        {
            var standing = evaluation.Standing[qualifier.TeamId];
            var row = qualifier.Reason == QualificationReason.Manual
                ? picks.FirstOrDefault(p => p.TeamId == qualifier.TeamId)
                : null;
            if (row is null)
            {
                row = StageQualification.Create(
                    request.ProgramId, request.StageId, qualifier.TeamId, qualifier.Reason, standing.Score, standing.Rank, actor,
                    toStage.Id, qualifier.SourceMatchId, qualifier.TieBreakEventId);
                _db.StageQualifications.Add(row);
            }

            row.Commit();
        }

        var created = 0;
        if (evaluation.Rule.SeedingMode != SeedingMode.Manual)
        {
            var alreadyPlaced = await (
                    from participant in _db.MatchParticipants
                    join match in _db.Matches on participant.MatchId equals match.Id
                    where match.StageId == toStage.Id
                    select participant.TeamId)
                .ToListAsync(cancellationToken);
            var ranked = evaluation.Qualifiers.Select(q => q.TeamId).Where(id => !alreadyPlaced.Contains(id)).ToList();
            if (ranked.Count > 0)
            {
                created = await _seeder.SeedAsync(request.ProgramId, toStage, ranked, evaluation.Rule.SeedingMode, actor, cancellationToken);
            }
        }

        var templateCount = await _db.StageSegmentTemplates.CountAsync(t => t.StageId == toStage.Id, cancellationToken);
        if (toStage.State == StageState.Draft && templateCount > 0)
        {
            toStage.MarkReady(templateCount);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
