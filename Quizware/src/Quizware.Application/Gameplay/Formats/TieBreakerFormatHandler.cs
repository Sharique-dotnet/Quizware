using System.Globalization;
using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Options when it has them; otherwise a typed answer checked by its
/// answer mode — exact text, or an exact number for numeric proximity.</summary>
public sealed class TieBreakerFormatHandler : OptionFormatHandler
{
    public TieBreakerFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.TieBreaker;

    public override async Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        if (selectedIds is not null || freeText is null)
        {
            return await base.EvaluateAsync(question, selectedIds, freeText, cancellationToken);
        }

        return question switch
        {
            TieBreakerQuestion { AnswerMode: TieBreakAnswerMode.NumericProximity, NumericAnswer: { } target }
                when decimal.TryParse(freeText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var given) && given == target
                => new ResponseEvaluation(true, false),
            TieBreakerQuestion { AnswerMode: TieBreakAnswerMode.ExactText } exact => AcceptedAnswers.Evaluate(freeText, exact.AnswerText, null),
            _ => ResponseEvaluation.NotChecked,
        };
    }
}
