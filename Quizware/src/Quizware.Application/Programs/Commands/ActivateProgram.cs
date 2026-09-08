using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;
using Quizware.Domain.Enums;

namespace Quizware.Application.Programs.Commands;

public sealed record ActivateProgramCommand(Guid ProgramId) : IRequest<ProgramDto>;

/// <summary>The frozen Phase 5 contract has one "start" endpoint but the
/// domain has two forward transitions before Live. This handler advances
/// Draft&#8594;Configured first if needed, then Configured&#8594;Live — one
/// call does what a Program Admin means by "make this live". Stage count
/// is a direct read of the Stage table (it exists since Phase 4) even
/// though Phase 7 hasn't built its own endpoints yet.</summary>
public sealed class ActivateProgramCommandHandler : IRequestHandler<ActivateProgramCommand, ProgramDto>
{
    private readonly IAppDbContext _db;

    public ActivateProgramCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgramDto> Handle(ActivateProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");

        if (program.State == ProgramState.Draft)
        {
            program.Configure();
        }

        var stageCount = await _db.Stages.CountAsync(s => s.ProgramId == request.ProgramId, cancellationToken);
        program.GoLive(stageCount);

        await _db.SaveChangesAsync(cancellationToken);

        return program.ToDto();
    }
}
