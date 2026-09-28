using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>The holder may pass the question on (see PassQuestion), under the
/// question's own MaxPassCount, PassDirection and RevealAnswerIfAllPass —
/// the columns that replace the legacy hardcoded PassStatus chain.</summary>
public sealed class PassingFormatHandler : OptionFormatHandler
{
    public PassingFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Passing;

    public override QuestionPassRules? PassRules(Question question) =>
        question is PassingQuestion passing
            ? new QuestionPassRules(passing.MaxPassCount, passing.PassDirection, passing.RevealAnswerIfAllPass)
            : null;
}
