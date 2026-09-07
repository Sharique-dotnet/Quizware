using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Commands;

public sealed record CompleteProgramCommand(Guid ProgramId) : IRequest<ProgramDto>;

public sealed class CompleteProgramCommandHandler : IRequestHandler<CompleteProgramCommand, ProgramDto>
{
    private readonly IAppDbContext _db;

    public CompleteProgramCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgramDto> Handle(CompleteProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");

        program.Complete();
        await _db.SaveChangesAsync(cancellationToken);

        return program.ToDto();
    }
}
