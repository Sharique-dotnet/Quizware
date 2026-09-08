using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

/// <summary>P7-11's readiness check: the program needs at least one stage,
/// and every stage needs at least one segment (<c>STAGE_HAS_NO_SEGMENTS</c>)
/// — checked only for formats actually in use, since no format is ever
/// compulsory (a stage with zero Passing segments is not a blocker; a stage
/// with zero segments of any kind is).</summary>
public sealed record ValidateProgramQuery(Guid ProgramId) : IRequest<ProgramValidationDto>;

public sealed class ValidateProgramQueryHandler : IRequestHandler<ValidateProgramQuery, ProgramValidationDto>
{
    private readonly IAppDbContext _db;

    public ValidateProgramQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgramValidationDto> Handle(ValidateProgramQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var stages = await _db.Stages.Where(s => s.ProgramId == request.ProgramId).ToListAsync(cancellationToken);
        if (stages.Count < 1)
        {
            return new ProgramValidationDto(false, ["Program needs at least one stage before it can go live."]);
        }

        var stageIds = stages.Select(s => s.Id).ToList();
        var segmentCountsByStage = await _db.StageSegmentTemplates
            .Where(s => stageIds.Contains(s.StageId))
            .GroupBy(s => s.StageId)
            .Select(g => new { StageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StageId, x => x.Count, cancellationToken);

        var blockers = stages
            .Where(s => !segmentCountsByStage.ContainsKey(s.Id) || segmentCountsByStage[s.Id] < 1)
            .Select(s => $"Stage '{s.Name}' has no segments (STAGE_HAS_NO_SEGMENTS).")
            .ToList();

        return blockers.Count == 0
            ? new ProgramValidationDto(true, [])
            : new ProgramValidationDto(false, blockers);
    }
}
