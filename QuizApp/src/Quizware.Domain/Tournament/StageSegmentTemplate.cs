using Quizware.Domain.Common;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Tournament;

/// <summary>What is played, in what order. Which question types are played is
/// decided entirely by which rows of this type exist for a stage — dropping a
/// format means simply not creating its row here.</summary>
public sealed class StageSegmentTemplate : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private StageSegmentTemplate()
    {
        CreatedBy = string.Empty;
    }

    public static StageSegmentTemplate Create(
        Guid programId, Guid stageId, QuestionFormatCode formatCode, int orderIndex,
        int questionCount, string createdBy)
    {
        if (questionCount < 1)
        {
            throw new ArgumentException("QuestionCount must be at least 1.", nameof(questionCount));
        }

        return new StageSegmentTemplate
        {
            ProgramId = programId,
            StageId = stageId,
            FormatCode = formatCode,
            OrderIndex = orderIndex,
            QuestionCount = questionCount,
            TopicSelectionMode = TopicSelectionMode.None,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid StageId { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public int OrderIndex { get; private set; }
    public bool IsOrderLocked { get; private set; }
    public string? DisplayName { get; private set; }
    public int QuestionCount { get; private set; }
    public int? QuestionsPerTeam { get; private set; }
    public int? TimeLimitSeconds { get; private set; }
    public bool IsOptional { get; private set; }
    public TopicSelectionMode TopicSelectionMode { get; private set; }
    public int? TopicChoiceLimit { get; private set; }
    public bool AllowPassing { get; private set; }
    public int? MaxPassCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void Reorder(int newOrderIndex, string updatedBy)
    {
        OrderIndex = newOrderIndex;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Lock(string updatedBy)
    {
        IsOrderLocked = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Update(int questionCount, bool isOrderLocked, string updatedBy)
    {
        if (questionCount < 1)
        {
            throw new ArgumentException("QuestionCount must be at least 1.", nameof(questionCount));
        }

        QuestionCount = questionCount;
        IsOrderLocked = isOrderLocked;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Delete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }
}
