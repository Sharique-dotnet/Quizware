using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>A clip or image with a typed answer. Once revealed, the reveal
/// media (if the question has one) replaces the original.</summary>
public sealed class AudioVisualFormatHandler : IQuestionFormatHandler
{
    public QuestionFormatCode Format => QuestionFormatCode.AudioVisual;

    public bool AnyTeamMayAnswer => false;

    public int? DefaultTimeLimitSeconds(Question question) => null;

    public bool WrongAnswerLeavesQuestionOpen(Question question) => false;

    public QuestionPassRules? PassRules(Question question) => null;

    public TopicChoice? TopicChoice(Question question) => null;

    public Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        var audioVisual = (AudioVisualQuestion)question;
        var media = matchQuestion.RevealedAtUtc is not null && audioVisual.RevealMediaAssetId is { } reveal
            ? reveal
            : audioVisual.MediaAssetId;
        return Task.FromResult(new FormatContent([], null, QuestionFormatHandlers.MediaUrl(media)));
    }

    public Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        var audioVisual = (AudioVisualQuestion)question;
        return Task.FromResult(AcceptedAnswers.Evaluate(freeText, audioVisual.AnswerText, audioVisual.AcceptableAnswersJson));
    }
}
