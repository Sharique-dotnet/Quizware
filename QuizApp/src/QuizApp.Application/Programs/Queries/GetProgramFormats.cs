using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

public sealed record GetProgramFormatsQuery(Guid ProgramId) : IRequest<IReadOnlyList<ProgramFormatDto>>;

public sealed class GetProgramFormatsQueryHandler
    : IRequestHandler<GetProgramFormatsQuery, IReadOnlyList<ProgramFormatDto>>
{
    private readonly IAppDbContext _db;

    public GetProgramFormatsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProgramFormatDto>> Handle(
        GetProgramFormatsQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var formats = await _db.ProgramQuestionFormats
            .Where(f => f.ProgramId == request.ProgramId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

        return formats.Select(f => f.ToDto()).ToList();
    }
}
