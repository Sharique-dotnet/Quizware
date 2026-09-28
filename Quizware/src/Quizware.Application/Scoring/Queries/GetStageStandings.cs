using MediatR;
using Quizware.Application.Scoring.Dtos;

namespace Quizware.Application.Scoring.Queries;

public sealed record GetStageStandingsQuery(Guid ProgramId, Guid StageId) : IRequest<StageStandingsDto>;

public sealed class GetStageStandingsQueryHandler : IRequestHandler<GetStageStandingsQuery, StageStandingsDto>
{
    private readonly Standings _standings;

    public GetStageStandingsQueryHandler(Standings standings)
    {
        _standings = standings;
    }

    public Task<StageStandingsDto> Handle(GetStageStandingsQuery request, CancellationToken cancellationToken) =>
        _standings.ForStageAsync(request.ProgramId, request.StageId, cancellationToken);
}
