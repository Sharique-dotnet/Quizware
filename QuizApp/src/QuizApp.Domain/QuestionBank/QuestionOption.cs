using QuizApp.Domain.Common;

namespace QuizApp.Domain.QuestionBank;

/// <summary>Shared by the six option-based formats: MCQ, Buzzer, Passing,
/// Card, Choice, TieBreaker.</summary>
public sealed class QuestionOption : BaseEntity, IAuditable, ISoftDeletable
{
    private QuestionOption()
    {
        OptionText = string.Empty;
        CreatedBy = string.Empty;
    }

    public static QuestionOption Create(
        Guid questionId, string optionText, bool isCorrect, int displayOrder, string createdBy, Guid? mediaAssetId = null)
    {
        if (string.IsNullOrWhiteSpace(optionText))
        {
            throw new ArgumentException("OptionText is required.", nameof(optionText));
        }

        return new QuestionOption
        {
            QuestionId = questionId,
            OptionText = optionText,
            IsCorrect = isCorrect,
            DisplayOrder = displayOrder,
            MediaAssetId = mediaAssetId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid QuestionId { get; private set; }
    public string OptionText { get; private set; }
    public bool IsCorrect { get; private set; }
    public int DisplayOrder { get; private set; }
    public Guid? MediaAssetId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
