using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

/// <summary>First pass on the readiness check: does the program have at
/// least one stage, the one thing <c>Program.GoLive</c> itself requires.
/// The richer per-format/segment coverage check
/// (<c>STAGE_HAS_NO_SEGMENTS</c>) is Phase 7's <c>P7-11</c>, which needs
/// the Tournament module's own data access — this handler is the same
/// endpoint's first, honest implementation, not the final one.</summary>
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

        var stageCount = await _db.Stages.CountAsync(s => s.ProgramId == request.ProgramId, cancellationToken);

        return stageCount < 1
            ? new ProgramValidationDto(false, ["Program needs at least one stage before it can go live."])
            : new ProgramValidationDto(true, []);
    }
}
