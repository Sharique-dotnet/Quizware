using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Programs.Dtos;

namespace QuizApp.Application.Programs.Queries;

/// <summary>Simple counts only for now — Warnings stays empty until a later
/// phase (e.g. Question Bank coverage) defines a real warning rule; an
/// empty list here is honest, not a placeholder pretending completeness.</summary>
public sealed record GetProgramDashboardQuery(Guid ProgramId) : IRequest<ProgramDashboardDto>;

public sealed class GetProgramDashboardQueryHandler : IRequestHandler<GetProgramDashboardQuery, ProgramDashboardDto>
{
    private readonly IAppDbContext _db;

    public GetProgramDashboardQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProgramDashboardDto> Handle(GetProgramDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            throw new KeyNotFoundException($"Program '{request.ProgramId}' was not found.");
        }

        var teamCount = await _db.Teams.CountAsync(t => t.ProgramId == request.ProgramId, cancellationToken);
        var questionCount = await _db.Questions.CountAsync(q => q.ProgramId == request.ProgramId, cancellationToken);
        var stageCount = await _db.Stages.CountAsync(s => s.ProgramId == request.ProgramId, cancellationToken);
        var matchCount = await _db.Matches.CountAsync(m => m.ProgramId == request.ProgramId, cancellationToken);

        return new ProgramDashboardDto(teamCount, questionCount, stageCount, matchCount, []);
    }
}
