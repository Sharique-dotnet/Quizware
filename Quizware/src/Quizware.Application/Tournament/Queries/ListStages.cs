using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Tournament.Dtos;

namespace Quizware.Application.Tournament.Queries;

public sealed record ListStagesQuery(Guid ProgramId) : IRequest<IReadOnlyList<StageSummaryDto>>;

public sealed class ListStagesQueryHandler : IRequestHandler<ListStagesQuery, IReadOnlyList<StageSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListStagesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StageSummaryDto>> Handle(ListStagesQuery request, CancellationToken cancellationToken)
    {
        var stages = await _db.Stages
            .Where(s => s.ProgramId == request.ProgramId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(cancellationToken);

        return stages.Select(s => s.ToSummaryDto()).ToList();
    }
}
