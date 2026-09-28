using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetMatchQuestionQuery(Guid MatchId, Guid MatchQuestionId) : IRequest<CurrentQuestionDto>;

public sealed class GetMatchQuestionQueryHandler : IRequestHandler<GetMatchQuestionQuery, CurrentQuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly LiveStateBuilder _state;

    public GetMatchQuestionQueryHandler(IAppDbContext db, LiveStateBuilder state)
    {
        _db = db;
        _state = state;
    }

    public async Task<CurrentQuestionDto> Handle(GetMatchQuestionQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        var question = await LiveRules.LoadQuestionAsync(_db, match.Id, request.MatchQuestionId, cancellationToken);
        return await _state.PresentAsync(question, cancellationToken);
    }
}
