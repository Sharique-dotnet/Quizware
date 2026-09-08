using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Teams.Dtos;
using QuizApp.Domain.Enums;

namespace QuizApp.Application.Teams.Queries;

public sealed record ListTeamsQuery(Guid ProgramId, TeamStatus? Status) : IRequest<IReadOnlyList<TeamSummaryDto>>;

public sealed class ListTeamsQueryHandler : IRequestHandler<ListTeamsQuery, IReadOnlyList<TeamSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListTeamsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TeamSummaryDto>> Handle(ListTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Teams.Where(t => t.ProgramId == request.ProgramId);
        if (request.Status is not null)
        {
            query = query.Where(t => t.Status == request.Status);
        }

        var teams = await query.OrderBy(t => t.SortOrder).ThenBy(t => t.Code).ToListAsync(cancellationToken);
        return teams.Select(t => t.ToSummaryDto()).ToList();
    }
}
