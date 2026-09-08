using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Programs.Dtos;

namespace Quizware.Application.Programs.Queries;

public sealed record GetProgramSettingsQuery(Guid ProgramId) : IRequest<IReadOnlyList<ProgramSettingDto>>;

public sealed class GetProgramSettingsQueryHandler
    : IRequestHandler<GetProgramSettingsQuery, IReadOnlyList<ProgramSettingDto>>
{
    private readonly IAppDbContext _db;

    public GetProgramSettingsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProgramSettingDto>> Handle(
        GetProgramSettingsQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var settings = await _db.ProgramSettings
            .Where(s => s.ProgramId == request.ProgramId)
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return settings.Select(s => s.ToDto()).ToList();
    }
}
