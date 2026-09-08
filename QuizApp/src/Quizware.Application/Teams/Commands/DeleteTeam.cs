using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Teams.Commands;

public sealed record DeleteTeamCommand(Guid ProgramId, Guid TeamId) : IRequest;

/// <summary>FR-2.6: a team already used in a match is never deletable,
/// only withdrawn. MatchParticipant exists since Phase 4 even though the
/// match engine (Phase 9) doesn't yet — this is a real check, not a
/// placeholder.</summary>
public sealed class DeleteTeamCommandHandler : IRequestHandler<DeleteTeamCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteTeamCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");

        var everPlayed = await _db.MatchParticipants.AnyAsync(mp => mp.TeamId == team.Id, cancellationToken);
        if (everPlayed)
        {
            throw new InvalidStateTransitionException(
                $"Team '{team.Code}' has already played in a match and cannot be deleted — withdraw it instead.");
        }

        team.Delete(_currentUser.Email ?? "unknown");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
