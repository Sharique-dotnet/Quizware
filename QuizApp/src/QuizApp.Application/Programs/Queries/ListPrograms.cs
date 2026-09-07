using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

public sealed record ListProgramsQuery : IRequest<IReadOnlyList<ProgramSummaryDto>>;

public sealed class ListProgramsQueryHandler : IRequestHandler<ListProgramsQuery, IReadOnlyList<ProgramSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListProgramsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProgramSummaryDto>> Handle(ListProgramsQuery request, CancellationToken cancellationToken)
    {
        var programs = await _db.Programs
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return programs.Select(p => p.ToSummaryDto()).ToList();
    }
}
