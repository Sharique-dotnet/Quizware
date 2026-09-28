using MediatR;
using Quizware.Application.Abstractions;

namespace Quizware.Application.Gameplay.Commands;

public sealed record RemoveMatchSegmentCommand(Guid ProgramId, Guid MatchId, Guid SegmentId) : IRequest;

/// <summary>Removes a segment before play and closes the gap it leaves, so
/// OrderIndex stays contiguous.</summary>
public sealed class RemoveMatchSegmentCommandHandler : IRequestHandler<RemoveMatchSegmentCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RemoveMatchSegmentCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveMatchSegmentCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var segment = segments.SingleOrDefault(s => s.Id == request.SegmentId)
            ?? throw new KeyNotFoundException($"Segment '{request.SegmentId}' was not found in this match.");

        segment.Delete(actor);
        await _db.SaveChangesAsync(cancellationToken);

        var remaining = segments.Where(s => s.Id != segment.Id).OrderBy(s => s.OrderIndex).ToList();
        await MatchSetup.ApplySegmentOrderAsync(
            _db, remaining.Select((s, i) => (s, i)).ToList(), cancellationToken);
    }
}
