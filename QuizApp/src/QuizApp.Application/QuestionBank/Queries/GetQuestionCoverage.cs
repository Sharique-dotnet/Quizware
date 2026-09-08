using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.QuestionBank.Dtos;
using QuizApp.Domain.Enums;

namespace QuizApp.Application.QuestionBank.Queries;

/// <summary>FR-3.9a: "never require questions for a format the program
/// does not use — coverage lists only formats that appear in a segment
/// template." This checks raw per-format counts against segment-template
/// requirements; it deliberately doesn't replicate the difficulty-mix/
/// repeat-policy logic that belongs to Phase 8's Question Selection
/// Engine — this is the CRUD phase's level of "do we have enough
/// questions", not the draw engine's.</summary>
public sealed record GetQuestionCoverageQuery(Guid ProgramId) : IRequest<QuestionCoverageDto>;

public sealed class GetQuestionCoverageQueryHandler : IRequestHandler<GetQuestionCoverageQuery, QuestionCoverageDto>
{
    private readonly IAppDbContext _db;

    public GetQuestionCoverageQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<QuestionCoverageDto> Handle(GetQuestionCoverageQuery request, CancellationToken cancellationToken)
    {
        var templatesByStage = await _db.StageSegmentTemplates
            .Where(t => t.ProgramId == request.ProgramId)
            .Join(_db.Stages, t => t.StageId, s => s.Id, (t, s) => new { s.Name, s.OrderIndex, t.FormatCode, t.QuestionCount })
            .ToListAsync(cancellationToken);

        var formatsInUse = templatesByStage.Select(t => t.FormatCode).Distinct().ToList();

        var enabledFormats = await _db.ProgramQuestionFormats
            .Where(f => f.ProgramId == request.ProgramId && f.IsEnabled)
            .Select(f => f.FormatCode)
            .ToListAsync(cancellationToken);
        var formatsNotUsed = enabledFormats.Where(f => !formatsInUse.Contains(f)).ToList();

        var availableByFormat = await _db.Questions
            .Where(q => q.ProgramId == request.ProgramId && q.Status == QuestionStatus.Approved && formatsInUse.Contains(q.FormatCode))
            .GroupBy(q => q.FormatCode)
            .Select(g => new { FormatCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.FormatCode, g => g.Count, cancellationToken);

        var blockers = new List<string>();
        var byStage = templatesByStage
            .GroupBy(t => new { t.Name, t.OrderIndex })
            .OrderBy(g => g.Key.OrderIndex)
            .Select(stageGroup =>
            {
                var requirements = stageGroup.Select(t =>
                {
                    var available = availableByFormat.GetValueOrDefault(t.FormatCode, 0);
                    var shortfall = t.QuestionCount - available;
                    var status = shortfall > 0 ? "Short" : "Ok";
                    if (shortfall > 0)
                    {
                        blockers.Add($"Stage '{stageGroup.Key.Name}' needs {t.QuestionCount} approved {t.FormatCode} questions but only {available} exist.");
                    }

                    return new QuestionCoverageRequirementDto(
                        t.FormatCode.ToString(), t.QuestionCount, available, status, shortfall > 0 ? shortfall : null);
                }).ToList();

                return new QuestionCoverageByStageDto(stageGroup.Key.Name, requirements);
            })
            .ToList();

        return new QuestionCoverageDto(
            ReadyToRun: blockers.Count == 0,
            FormatsInUse: formatsInUse.Select(f => f.ToString()).ToList(),
            FormatsNotUsed: formatsNotUsed.Select(f => f.ToString()).ToList(),
            ByStage: byStage,
            Blockers: blockers);
    }
}
