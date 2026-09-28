using MediatR;
using Quizware.Application.Scoring.Dtos;

namespace Quizware.Application.Scoring.Queries;

public sealed record GetTeamStandingQuery(Guid ProgramId, Guid TeamId) : IRequest<TeamStandingDto>;

public sealed class GetTeamStandingQueryHandler : IRequestHandler<GetTeamStandingQuery, TeamStandingDto>
{
    private readonly Standings _standings;

    public GetTeamStandingQueryHandler(Standings standings)
    {
        _standings = standings;
    }

    public Task<TeamStandingDto> Handle(GetTeamStandingQuery request, CancellationToken cancellationToken) =>
        _standings.ForTeamAsync(request.ProgramId, request.TeamId, cancellationToken);
}
