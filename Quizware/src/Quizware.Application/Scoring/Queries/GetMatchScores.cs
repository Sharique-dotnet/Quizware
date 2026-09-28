using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Scoring.Dtos;

namespace Quizware.Application.Scoring.Queries;

public sealed record GetMatchScoresQuery(Guid MatchId) : IRequest<IReadOnlyList<TeamScoreItemDto>>;

public sealed class GetMatchScoresQueryHandler : IRequestHandler<GetMatchScoresQuery, IReadOnlyList<TeamScoreItemDto>>
{
    private readonly IAppDbContext _db;

    public GetMatchScoresQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TeamScoreItemDto>> Handle(GetMatchScoresQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchScoreboard.LoadMatchAsync(_db, request.MatchId, cancellationToken);
        return await MatchScoreboard.BuildAsync(_db, match, cancellationToken);
    }
}
