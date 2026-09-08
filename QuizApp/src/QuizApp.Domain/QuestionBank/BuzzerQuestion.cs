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
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        int buzzWindowSeconds = 30, bool lockoutOnWrongAnswer = true, bool allowStealAfterWrong = true,
        int? stealWindowSeconds = null)
    {
        return new BuzzerQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            BuzzWindowSeconds = buzzWindowSeconds,
            LockoutOnWrongAnswer = lockoutOnWrongAnswer,
            AllowStealAfterWrong = allowStealAfterWrong,
            StealWindowSeconds = stealWindowSeconds,
        };
    }

    public int BuzzWindowSeconds { get; private set; }
    public bool LockoutOnWrongAnswer { get; private set; }
    public bool AllowStealAfterWrong { get; private set; }
    public int? StealWindowSeconds { get; private set; }
}
