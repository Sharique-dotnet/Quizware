using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Queries;

public sealed record GetMatchByIdQuery(Guid ProgramId, Guid MatchId) : IRequest<MatchDto>;

public sealed class GetMatchByIdQueryHandler : IRequestHandler<GetMatchByIdQuery, MatchDto>
{
    private readonly IAppDbContext _db;

    public GetMatchByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<MatchDto> Handle(GetMatchByIdQuery request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        return await MatchSetup.ToDetailAsync(_db, match, cancellationToken);
    }
}
