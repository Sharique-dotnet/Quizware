using FluentValidation;
using MediatR;
using Quizware.Application.Rules.Dtos;
using Quizware.Application.Selection;
using Quizware.Domain.Enums;

namespace Quizware.Application.Rules.Queries;

/// <summary>Phase 8: a dry run through the real <see cref="IQuestionSelector"/>
/// draw — pool building, repeat-policy exclusion, difficulty mix, and the
/// fallback ladder all run exactly as they would for a real reservation, so
/// this preview can never disagree with what a match start would actually
/// draw. Nothing is written; <see cref="IQuestionSelector.PreviewAsync"/>
/// never throws on exhaustion, it reports <c>CanSatisfy = false</c> instead.</summary>
public sealed record PreviewSelectionQuery(
    Guid ProgramId, Guid StageId, Guid? SegmentTemplateId, string FormatCode, int QuestionCount)
    : IRequest<SelectionPreviewResultDto>;

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
    private readonly IQuestionSelector _selector;

    public PreviewSelectionQueryHandler(IQuestionSelector selector)
    {
        _selector = selector;
    }

    public async Task<SelectionPreviewResultDto> Handle(PreviewSelectionQuery request, CancellationToken cancellationToken)
    {
        var formatCode = Enum.Parse<QuestionFormatCode>(request.FormatCode, ignoreCase: true);

        var result = await _selector.PreviewAsync(
            new SelectionRequest(request.ProgramId, request.StageId, request.SegmentTemplateId, formatCode, request.QuestionCount, RandomSeed: 1),
            cancellationToken);

        return new SelectionPreviewResultDto(
            result.PoolSize, result.EligibleAfterFilters, result.EligibleAfterRepeatPolicy,
            result.DifficultyMixRequested, result.DifficultyMixAchieved, result.CanSatisfy, result.Warnings);
    }
}
