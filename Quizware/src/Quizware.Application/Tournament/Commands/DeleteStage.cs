using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Tournament.Commands;

public sealed record DeleteStageCommand(Guid ProgramId, Guid StageId) : IRequest;

/// <summary>A stage with any match already created cannot be deleted —
/// retire it via the normal state machine instead. Matches only ever exist
/// once a stage has left Draft, so this also naturally blocks deleting a
/// stage that is Ready/InProgress/Completed and already has scheduled play.</summary>
public sealed class DeleteStageCommandHandler : IRequestHandler<DeleteStageCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteStageCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var hasMatches = await _db.Matches.AnyAsync(m => m.StageId == stage.Id, cancellationToken);
        if (hasMatches)
        {
            throw new InvalidStateTransitionException($"Stage '{stage.Name}' already has matches and cannot be deleted.");
        }

        var actor = _currentUser.Email ?? "unknown";
        var segments = await _db.StageSegmentTemplates.Where(s => s.StageId == stage.Id).ToListAsync(cancellationToken);
        foreach (var segment in segments)
        {
            segment.Delete(actor);
        }

        stage.Delete(actor);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
