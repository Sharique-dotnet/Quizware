using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification;

namespace Quizware.Application.Gameplay.Commands;

/// <summary>Only a match that has not started can be deleted — a started
/// match is abandoned instead so its history survives.</summary>
public sealed record DeleteMatchCommand(Guid ProgramId, Guid MatchId) : IRequest;

public sealed class DeleteMatchCommandHandler : IRequestHandler<DeleteMatchCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TieBreakMatches _tieBreaks;

    public DeleteMatchCommandHandler(IAppDbContext db, ICurrentUser currentUser, TieBreakMatches tieBreaks)
    {
        _db = db;
        _currentUser = currentUser;
        _tieBreaks = tieBreaks;
    }

    public async Task Handle(DeleteMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";

        match.Delete(actor);
        await _tieBreaks.ReleaseAsync(match, cancellationToken);

        foreach (var participant in await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken))
        {
            participant.Delete(actor);
        }

        foreach (var segment in await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken))
        {
            segment.Delete(actor);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
