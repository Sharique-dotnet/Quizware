using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetLiveMatchStateQuery(Guid MatchId) : IRequest<LiveMatchStateDto>;

public sealed class GetLiveMatchStateQueryHandler : IRequestHandler<GetLiveMatchStateQuery, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly LiveStateBuilder _state;

    public GetLiveMatchStateQueryHandler(IAppDbContext db, LiveStateBuilder state)
    {
        _db = db;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(GetLiveMatchStateQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        return await _state.BuildAsync(match, cancellationToken);
    }
}
