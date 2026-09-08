using FluentValidation;

namespace Quizware.Api.Contracts.V1.Questions.Formats;

/// <summary>Shared rules included by every per-format validator (05-API-Design.md §5.8).</summary>
public sealed class QuestionBaseValidator : AbstractValidator<CreateQuestionRequestBase>
{
    public QuestionBaseValidator()
    {
        RuleFor(x => x.DifficultyLevelId).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(10);
        RuleFor(x => x.TimeLimitSeconds).GreaterThan(0).When(x => x.TimeLimitSeconds.HasValue);
    }
}

file static class OptionRules
{
    public static IRuleBuilderOptions<T, List<OptionDto>> MustHaveBetween2And8Options<T>(this IRuleBuilder<T, List<OptionDto>> rule) =>
        rule.Must(o => o.Count is >= 2 and <= 8).WithMessage("Between 2 and 8 options are required.");
}

public sealed class CreateMcqQuestionValidator : AbstractValidator<CreateMcqQuestionRequest>
{
    public CreateMcqQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options();
        RuleFor(x => x.Options)
            .Must(o => o.Count(p => p.IsCorrect) == 1)
            .WithMessage("Exactly one option must be marked correct.")
            .Unless(x => x.AllowMultipleCorrect);
        RuleForEach(x => x.Options).ChildRules(o => o.RuleFor(p => p.Text).NotEmpty().MaximumLength(1000));
    }
}

public sealed class CreateBuzzerQuestionValidator : AbstractValidator<CreateBuzzerQuestionRequest>
{
    public CreateBuzzerQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options();
        RuleFor(x => x.BuzzWindowSeconds).GreaterThan(0);
        RuleFor(x => x.StealWindowSeconds).GreaterThan(0).When(x => x.StealWindowSeconds.HasValue);
    }
}

public sealed class CreatePassingQuestionValidator : AbstractValidator<CreatePassingQuestionRequest>
{
    public CreatePassingQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options();
        RuleFor(x => x.MaxPassCount).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateCardQuestionValidator : AbstractValidator<CreateCardQuestionRequest>
{
    public CreateCardQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options();
    }
}

public sealed class CreateChoiceQuestionValidator : AbstractValidator<CreateChoiceQuestionRequest>
{
    public CreateChoiceQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options();
        RuleFor(x => x.TopicLabel).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TopicChoiceLimit).GreaterThan(0).When(x => x.TopicChoiceLimit.HasValue);
    }
}

public sealed class CreateRapidFireQuestionValidator : AbstractValidator<CreateRapidFireQuestionRequest>
{
    public CreateRapidFireQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000).Unless(x => x.IsHostRead);
        RuleFor(x => x.AnswerText).NotEmpty().WithMessage("AnswerText is required unless the question is host-read.")
            .Unless(x => x.IsHostRead);
    }
}

public sealed class CreateTieBreakerQuestionValidator : AbstractValidator<CreateTieBreakerQuestionRequest>
{
    public CreateTieBreakerQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Options).MustHaveBetween2And8Options().When(x => x.AnswerMode == Quizware.Domain.Enums.TieBreakAnswerMode.Options);
        RuleFor(x => x.AnswerText).NotEmpty()
            .When(x => x.AnswerMode == Quizware.Domain.Enums.TieBreakAnswerMode.ExactText);
        RuleFor(x => x.NumericAnswer).NotNull()
            .When(x => x.AnswerMode == Quizware.Domain.Enums.TieBreakAnswerMode.NumericProximity);
    }
}

public sealed class CreateSequenceQuestionValidator : AbstractValidator<CreateSequenceQuestionRequest>
{
    public CreateSequenceQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.Items).Must(i => i.Count >= 2).WithMessage("A sequence needs at least 2 items.");
        RuleFor(x => x.Items)
            .Must(BeContiguousFromOne)
            .WithMessage("Correct positions must be 1..N with no gaps or duplicates.");
        RuleFor(x => x.PointsPerCorrectPosition)
            .NotNull()
            .When(x => x.PartialCreditEnabled)
            .WithMessage("Partial credit requires a points value per position.");
    }

    private static bool BeContiguousFromOne(List<SequenceItemDto> items) =>
        items.Select(i => i.CorrectPosition).OrderBy(p => p).SequenceEqual(Enumerable.Range(1, items.Count));
}

public sealed class CreateAudioVisualQuestionValidator : AbstractValidator<CreateAudioVisualQuestionRequest>
{
    public CreateAudioVisualQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.MediaAssetId).NotEmpty();
        RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PlaybackDurationSeconds).GreaterThan(0).When(x => x.PlaybackDurationSeconds.HasValue);
    }
}

public sealed class CreateVisualRapidFireQuestionValidator : AbstractValidator<CreateVisualRapidFireQuestionRequest>
{
    public CreateVisualRapidFireQuestionValidator()
    {
        Include(new QuestionBaseValidator());

        RuleFor(x => x.Items).Must(i => i.Count >= 2).WithMessage("Visual rapid fire needs at least 2 images.");
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(p => p.AnswerText).NotEmpty());
    }
}
