using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetMatchSegmentsQuery(Guid? ProgramId, Guid MatchId) : IRequest<IReadOnlyList<MatchSegmentDto>>;

public sealed class GetMatchSegmentsQueryHandler : IRequestHandler<GetMatchSegmentsQuery, IReadOnlyList<MatchSegmentDto>>
{
    private readonly IAppDbContext _db;

    public GetMatchSegmentsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MatchSegmentDto>> Handle(GetMatchSegmentsQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        return segments.Select(MatchSetup.ToDto).ToList();
    }
}
