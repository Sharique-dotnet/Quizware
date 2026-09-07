using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

public sealed record GetProgramByIdQuery(Guid ProgramId) : IRequest<ProgramDto>;

public sealed class GetProgramByIdQueryHandler : IRequestHandler<GetProgramByIdQuery, ProgramDto>
{
    private readonly IAppDbContext _db;

    public GetProgramByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgramDto> Handle(GetProgramByIdQuery request, CancellationToken cancellationToken)
    {
        var program = await _db.Programs.SingleOrDefaultAsync(p => p.Id == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");

        return program.ToDto();
    }
}
