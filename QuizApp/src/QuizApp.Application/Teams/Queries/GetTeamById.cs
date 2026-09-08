using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Teams.Dtos;

namespace QuizApp.Application.Teams.Queries;

public sealed record GetTeamByIdQuery(Guid ProgramId, Guid TeamId) : IRequest<TeamDto>;

public sealed class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDto>
{
    private readonly IAppDbContext _db;

    public GetTeamByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TeamDto> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");

        var members = await _db.TeamMembers.Where(m => m.TeamId == team.Id).ToListAsync(cancellationToken);
        return team.ToDto(members);
    }
}
