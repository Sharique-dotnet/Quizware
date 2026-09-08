using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

public sealed class ChoiceQuestion : Question
{
    private ChoiceQuestion()
    {
        TopicLabel = string.Empty;
    }

    private ChoiceQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId, string topicLabel)
        : base(programId, ownerScope, QuestionFormatCode.Choice, questionText, difficultyLevel, language, createdBy, topicId)
    {
        TopicLabel = topicLabel;
    }

    public static ChoiceQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText, string topicLabel,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        bool isTopicExclusive = true, int? topicDisplayOrder = null)
    {
        if (string.IsNullOrWhiteSpace(topicLabel))
        {
            throw new ArgumentException("TopicLabel is required for a Choice question.", nameof(topicLabel));
        }

        return new ChoiceQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId, topicLabel)
        {
            IsTopicExclusive = isTopicExclusive,
            TopicDisplayOrder = topicDisplayOrder,
        };
    }

    public string TopicLabel { get; private set; }
    public bool IsTopicExclusive { get; private set; }
    public int? TopicDisplayOrder { get; private set; }
}
