using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>Quick-fire with typed answers: any team may answer (BR-2.4) and a
/// wrong answer leaves the question open to the rest. The burst length, when
/// set, is the default timer.</summary>
public sealed class RapidFireFormatHandler : IQuestionFormatHandler
{
    public QuestionFormatCode Format => QuestionFormatCode.RapidFire;

    public bool AnyTeamMayAnswer => true;

    public int? DefaultTimeLimitSeconds(Question question) => (question as RapidFireQuestion)?.BurstSeconds;

    public bool WrongAnswerLeavesQuestionOpen(Question question) => true;

    public QuestionPassRules? PassRules(Question question) => null;

    public TopicChoice? TopicChoice(Question question) => null;

    public Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken) =>
        Task.FromResult(new FormatContent([], null, null));

    public Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        var rapidFire = (RapidFireQuestion)question;
        return Task.FromResult(AcceptedAnswers.Evaluate(freeText, rapidFire.AnswerText, rapidFire.AcceptableAnswersJson));
    }
}
