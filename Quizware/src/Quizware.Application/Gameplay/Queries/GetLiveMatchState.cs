using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

/// <summary>The live state for the calling user's audience, unless
/// <paramref name="Audience"/> forces one (the display hub always asks for Display).</summary>
public sealed record GetLiveMatchStateQuery(Guid MatchId, LiveAudience? Audience = null) : IRequest<LiveMatchStateDto>;

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
        return await _state.BuildAsync(match, request.Audience ?? _state.CallerAudience, cancellationToken);
    }
}
