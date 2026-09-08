using Quizware.Domain.Enums;

namespace Quizware.Domain.QuestionBank;

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
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        bool allowMultipleCorrect = false, bool shuffleOptions = true, bool negativeMarkingEnabled = false)
    {
        return new McqQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            AllowMultipleCorrect = allowMultipleCorrect,
            ShuffleOptions = shuffleOptions,
            NegativeMarkingEnabled = negativeMarkingEnabled,
        };
    }

    public bool AllowMultipleCorrect { get; private set; }
    public bool ShuffleOptions { get; private set; }
    public bool NegativeMarkingEnabled { get; private set; }
}
