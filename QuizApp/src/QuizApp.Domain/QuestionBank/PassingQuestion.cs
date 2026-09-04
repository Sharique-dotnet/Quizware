using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

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
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new PassingQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            MaxPassCount = 2,
            PassDirection = PassDirection.Clockwise,
            RevealAnswerIfAllPass = true,
        };
    }

    public int MaxPassCount { get; private set; }
    public PassDirection PassDirection { get; private set; }
    public bool RevealAnswerIfAllPass { get; private set; }
}
