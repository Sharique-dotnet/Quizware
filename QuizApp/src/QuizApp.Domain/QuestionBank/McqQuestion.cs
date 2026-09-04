using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

public sealed class McqQuestion : Question
{
    private McqQuestion()
    {
    }

    private McqQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.Mcq, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static McqQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new McqQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            ShuffleOptions = true,
        };
    }

    public bool AllowMultipleCorrect { get; private set; }
    public bool ShuffleOptions { get; private set; }
    public bool NegativeMarkingEnabled { get; private set; }
}
