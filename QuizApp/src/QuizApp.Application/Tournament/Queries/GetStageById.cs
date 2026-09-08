using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Tournament.Dtos;

namespace QuizApp.Application.Tournament.Queries;

public sealed record GetStageByIdQuery(Guid ProgramId, Guid StageId) : IRequest<StageDto>;

public sealed class GetStageByIdQueryHandler : IRequestHandler<GetStageByIdQuery, StageDto>
{
    private readonly IAppDbContext _db;

    public GetStageByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<StageDto> Handle(GetStageByIdQuery request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var segments = await _db.StageSegmentTemplates.Where(s => s.StageId == stage.Id).ToListAsync(cancellationToken);
        return stage.ToDto(segments);
    }
}

public sealed record ListSegmentsQuery(Guid ProgramId, Guid StageId) : IRequest<IReadOnlyList<SegmentTemplateDto>>;

public sealed class ListSegmentsQueryHandler : IRequestHandler<ListSegmentsQuery, IReadOnlyList<SegmentTemplateDto>>
{
    private readonly IAppDbContext _db;

    public ListSegmentsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SegmentTemplateDto>> Handle(ListSegmentsQuery request, CancellationToken cancellationToken)
    {
        var stageExists = await _db.Stages.AnyAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken);
        if (!stageExists)
        {
            throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");
        }

        var segments = await _db.StageSegmentTemplates
            .Where(s => s.StageId == request.StageId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(cancellationToken);

        return segments.Select(s => s.ToDto()).ToList();
    }
}
