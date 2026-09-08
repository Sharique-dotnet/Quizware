using Quizware.Domain.Enums;

namespace Quizware.Domain.QuestionBank;

public sealed class PassingQuestion : Question
{
    private PassingQuestion()
    {
    }

    private PassingQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.Passing, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static PassingQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        int maxPassCount = 2, PassDirection passDirection = PassDirection.Clockwise, bool revealAnswerIfAllPass = true)
    {
        return new PassingQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            MaxPassCount = maxPassCount,
            PassDirection = passDirection,
            RevealAnswerIfAllPass = revealAnswerIfAllPass,
        };
    }

    public int MaxPassCount { get; private set; }
    public PassDirection PassDirection { get; private set; }
    public bool RevealAnswerIfAllPass { get; private set; }
}
