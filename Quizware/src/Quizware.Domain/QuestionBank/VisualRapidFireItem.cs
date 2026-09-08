using Quizware.Domain.Common;

namespace Quizware.Domain.QuestionBank;

public sealed class VisualRapidFireItem : BaseEntity, IAuditable, ISoftDeletable
{
    private VisualRapidFireItem()
    {
        AnswerText = string.Empty;
        CreatedBy = string.Empty;
    }

    public static VisualRapidFireItem Create(
        Guid questionId, Guid mediaAssetId, string answerText, int displayOrder, string createdBy)
    {
        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("MediaAssetId is required.", nameof(mediaAssetId));
        }

        if (string.IsNullOrWhiteSpace(answerText))
        {
            throw new ArgumentException("AnswerText is required.", nameof(answerText));
        }

        return new VisualRapidFireItem
        {
            QuestionId = questionId,
            MediaAssetId = mediaAssetId,
            AnswerText = answerText,
            DisplayOrder = displayOrder,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid QuestionId { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public string AnswerText { get; private set; }
    public string? AcceptableAnswersJson { get; private set; }
    public int DisplayOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
