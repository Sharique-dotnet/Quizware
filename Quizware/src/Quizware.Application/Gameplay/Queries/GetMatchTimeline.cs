using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetMatchTimelineQuery(Guid MatchId) : IRequest<IReadOnlyList<MatchEventItemDto>>;

/// <summary>MatchEvent is not tenant-filtered itself, so the match is loaded
/// first — that lookup is what keeps another program's timeline out of reach.</summary>
public sealed class GetMatchTimelineQueryHandler : IRequestHandler<GetMatchTimelineQuery, IReadOnlyList<MatchEventItemDto>>
{
    private readonly IAppDbContext _db;

    public GetMatchTimelineQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MatchEventItemDto>> Handle(GetMatchTimelineQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        return await _db.MatchEvents
            .Where(e => e.MatchId == match.Id)
            .OrderBy(e => e.SequenceNumber)
            .Select(e => new MatchEventItemDto(e.SequenceNumber, e.EventType, e.PayloadJson, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }
}
