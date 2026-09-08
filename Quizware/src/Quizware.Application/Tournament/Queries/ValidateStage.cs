using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Tournament.Dtos;

namespace Quizware.Application.Tournament.Queries;

/// <summary>P7-11's single-stage view: the same STAGE_HAS_NO_SEGMENTS check
/// the program-wide readiness endpoint (ValidateProgram) runs for every
/// stage, scoped to just this one.</summary>
public sealed record ValidateStageQuery(Guid ProgramId, Guid StageId) : IRequest<StageValidationDto>;

public sealed class ValidateStageQueryHandler : IRequestHandler<ValidateStageQuery, StageValidationDto>
{
    private readonly IAppDbContext _db;

    public ValidateStageQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<StageValidationDto> Handle(ValidateStageQuery request, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages
            .SingleOrDefaultAsync(s => s.Id == request.StageId && s.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{request.StageId}' was not found.");

        var segmentCount = await _db.StageSegmentTemplates.CountAsync(s => s.StageId == stage.Id, cancellationToken);

        return segmentCount < 1
            ? new StageValidationDto(false, [$"Stage '{stage.Name}' has no segments (STAGE_HAS_NO_SEGMENTS)."])
            : new StageValidationDto(true, []);
    }
}
