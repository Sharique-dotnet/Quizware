using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

public sealed class BuzzerQuestion : Question
{
    private BuzzerQuestion()
    {
    }

    private BuzzerQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.Buzzer, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static BuzzerQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new BuzzerQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            BuzzWindowSeconds = 30,
            LockoutOnWrongAnswer = true,
            AllowStealAfterWrong = true,
        };
    }

    public int BuzzWindowSeconds { get; private set; }
    public bool LockoutOnWrongAnswer { get; private set; }
    public bool AllowStealAfterWrong { get; private set; }
    public int? StealWindowSeconds { get; private set; }
}
