using MediatR;
using Quizware.Application.Abstractions;

namespace Quizware.Application.Gameplay.Commands;

/// <summary>Setup-time removal. During play a team leaves by disqualification
/// instead, which keeps its record.</summary>
public sealed record RemoveMatchParticipantCommand(Guid ProgramId, Guid MatchId, Guid ParticipantId) : IRequest;

public sealed class RemoveMatchParticipantCommandHandler : IRequestHandler<RemoveMatchParticipantCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RemoveMatchParticipantCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveMatchParticipantCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var participant = participants.SingleOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new KeyNotFoundException($"Participant '{request.ParticipantId}' was not found in this match.");

        participant.Delete(actor);
        MatchSetup.RecompactTurnOrder(participants.Where(p => p.Id != participant.Id).ToList());
        await _db.SaveChangesAsync(cancellationToken);
    }
}
