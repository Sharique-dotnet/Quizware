using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Rules.Dtos;
using QuizApp.Domain.Enums;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.Rules.Queries;

/// <summary>A pool-size preview only — it reports how many Approved
/// questions of the requested format exist (shared library included) and
/// their difficulty spread. The actual draw algorithm (repeat policy across
/// matches, topic spread, per-team fairness) is <c>IQuestionSelector</c>,
/// Phase 8's own interface; this handler exists so P7 configuration screens
/// can sanity-check a rule against the current bank size before Phase 8
/// exists.</summary>
public sealed record PreviewSelectionQuery(Guid ProgramId, Guid StageId, string FormatCode, int QuestionCount) : IRequest<SelectionPreviewResultDto>;

public sealed class PreviewSelectionQueryValidator : AbstractValidator<PreviewSelectionQuery>
{
    public PreviewSelectionQueryValidator()
    {
        RuleFor(x => x.QuestionCount).GreaterThanOrEqualTo(1);
        RuleFor(x => x.FormatCode).Must(f => Enum.TryParse<QuestionFormatCode>(f, ignoreCase: true, out _))
            .WithMessage("FormatCode is not a recognized question format.");
    }
}

public sealed class PreviewSelectionQueryHandler : IRequestHandler<PreviewSelectionQuery, SelectionPreviewResultDto>
{
    private readonly IAppDbContext _db;

    public PreviewSelectionQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<SelectionPreviewResultDto> Handle(PreviewSelectionQuery request, CancellationToken cancellationToken)
    {
        var formatCode = Enum.Parse<QuestionFormatCode>(request.FormatCode, ignoreCase: true);

        var pool = await _db.Questions
            .Where(q => (q.ProgramId == request.ProgramId || q.ProgramId == null) && q.FormatCode == formatCode)
            .ToListAsync(cancellationToken);
        var eligible = pool.Where(q => q.Status == QuestionStatus.Approved).ToList();

        var achievableMix = eligible
            .GroupBy(q => q.DifficultyLevel)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var warnings = new List<string>();
        var canSatisfy = eligible.Count >= request.QuestionCount;
        if (!canSatisfy)
        {
            warnings.Add($"Only {eligible.Count} approved question(s) available for {formatCode}; {request.QuestionCount} requested.");
        }

        return new SelectionPreviewResultDto(
            pool.Count,
            eligible.Count,
            eligible.Count,
            new Dictionary<string, int>(),
            achievableMix,
            canSatisfy,
            warnings);
    }
}
