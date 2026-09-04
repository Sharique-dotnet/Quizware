using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

public sealed class CardQuestion : Question
{
    private CardQuestion()
    {
    }

    private CardQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.Card, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static CardQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new CardQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            CardCount = 4,
            CardRevealMode = CardRevealMode.AllAtOnce,
        };
    }

    public int CardCount { get; private set; }
    public CardRevealMode CardRevealMode { get; private set; }
    public string? CardLabel { get; private set; }
}
