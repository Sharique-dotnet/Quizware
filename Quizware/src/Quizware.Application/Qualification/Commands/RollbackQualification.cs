using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Qualification.Commands;

public sealed record RollbackQualificationCommand(Guid ProgramId, Guid StageId) : IRequest;

/// <summary>P11-08: undoes a commit while the next stage has not started — no
/// match there has gone past setup. The matches the commit created (unstarted
/// matches made up only of this stage's qualifiers) are deleted, the
/// committed rows are removed, and manual wildcard picks are kept, uncommitted,
/// for the next commit. The next stage goes back to Draft if the commit made it Ready.</summary>
public sealed class RollbackQualificationCommandHandler : IRequestHandler<RollbackQualificationCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RollbackQualificationCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(RollbackQualificationCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        if (!await _db.Stages.AnyAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");
        }

        var committed = await _db.StageQualifications
            .Where(q => q.FromStageId == request.StageId && q.IsCommitted)
            .ToListAsync(cancellationToken);
        if (committed.Count == 0)
        {
            throw new InvalidStateTransitionException("Nothing is committed from this stage, so there is nothing to roll back.");
        }

        var toStageId = committed.Select(q => q.ToStageId).FirstOrDefault(id => id is not null);
        if (toStageId is { } nextStageId)
        {
            var nextStage = await _db.Stages.SingleAsync(s => s.Id == nextStageId, cancellationToken);
            var matches = await _db.Matches
                .Where(m => m.StageId == nextStageId && m.MatchKind == MatchKind.Regular)
                .ToListAsync(cancellationToken);
            var started = matches.FirstOrDefault(m => !m.IsInSetup);
            if (started is not null || nextStage.State is StageState.InProgress or StageState.Completed)
            {
                throw new InvalidStateTransitionException(
                    $"Stage '{nextStage.Name}' has already started{(started is null ? string.Empty : $" (match {started.MatchNumber} is {started.State})")}; its qualification can no longer be rolled back.");
            }

            var qualified = committed.Select(q => q.TeamId).ToHashSet();
            foreach (var match in matches)
            {
                var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
                if (participants.Count == 0 || !participants.All(p => qualified.Contains(p.TeamId)))
                {
                    continue;
                }

                match.Delete(actor);
                foreach (var participant in participants)
                {
                    participant.Delete(actor);
                }

                foreach (var segment in await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken))
                {
                    segment.Delete(actor);
                }
            }

            if (nextStage.State == StageState.Ready)
            {
                nextStage.ReturnToDraft();
            }
        }

        foreach (var row in committed)
        {
            if (row.QualificationReason == QualificationReason.Manual)
            {
                row.Uncommit(actor);
            }
            else
            {
                row.Delete(actor);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
