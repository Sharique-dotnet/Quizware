using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Any team may buzz in (BR-2.4). The buzz window is the default
/// timer, and a wrong answer leaves the question open only when the question
/// allows a steal.</summary>
public sealed class BuzzerFormatHandler : OptionFormatHandler
{
    public BuzzerFormatHandler(IAppDbContext db)
        : base(db)
    {
    }

    public override QuestionFormatCode Format => QuestionFormatCode.Buzzer;

    public override bool AnyTeamMayAnswer => true;

    public override int? DefaultTimeLimitSeconds(Question question) => (question as BuzzerQuestion)?.BuzzWindowSeconds;

    public override bool WrongAnswerLeavesQuestionOpen(Question question) => question is BuzzerQuestion { AllowStealAfterWrong: true };
}
