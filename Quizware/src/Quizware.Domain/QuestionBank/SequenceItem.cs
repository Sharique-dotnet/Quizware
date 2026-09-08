using Quizware.Domain.Common;

namespace Quizware.Domain.QuestionBank;

/// <summary>Separate from QuestionOption because a sequence element carries a
/// correct position, not a correct/incorrect flag.</summary>
public sealed class SequenceItem : BaseEntity, IAuditable, ISoftDeletable
{
    private SequenceItem()
    {
        CreatedBy = string.Empty;
    }

    public static SequenceItem Create(
        Guid questionId, int correctPosition, int displayOrder, string createdBy,
        string? itemText = null, Guid? mediaAssetId = null)
    {
        if (correctPosition < 1)
        {
            throw new ArgumentException("CorrectPosition is 1-based.", nameof(correctPosition));
        }

        return new SequenceItem
        {
            QuestionId = questionId,
            CorrectPosition = correctPosition,
            DisplayOrder = displayOrder,
            ItemText = itemText,
            MediaAssetId = mediaAssetId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid QuestionId { get; private set; }
    public string? ItemText { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public int CorrectPosition { get; private set; }
    public int DisplayOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
