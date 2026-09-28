using MediatR;
using Quizware.Application.Scoring.Dtos;

namespace Quizware.Application.Scoring.Queries;

public sealed record GetOverallStandingsQuery(Guid ProgramId) : IRequest<IReadOnlyList<StandingEntryDto>>;

public sealed class GetOverallStandingsQueryHandler : IRequestHandler<GetOverallStandingsQuery, IReadOnlyList<StandingEntryDto>>
{
    private readonly Standings _standings;

    public GetOverallStandingsQueryHandler(Standings standings)
    {
        _standings = standings;
    }

    public Task<IReadOnlyList<StandingEntryDto>> Handle(GetOverallStandingsQuery request, CancellationToken cancellationToken) =>
        _standings.OverallAsync(request.ProgramId, cancellationToken);
}
