using Quizware.Domain.Common;

namespace Quizware.Domain.QuestionBank;

/// <summary>Drives question selection and the Choice round's topic list.
/// ProgramId null means shared across programs.</summary>
public sealed class Topic : BaseEntity, IAuditable, ISoftDeletable
{
    private Topic()
    {
        Name = string.Empty;
        CreatedBy = string.Empty;
    }

    public static Topic Create(string name, string createdBy, Guid? programId = null, Guid? parentTopicId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Topic
        {
            ProgramId = programId,
            Name = name,
            ParentTopicId = parentTopicId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid? ProgramId { get; private set; }
    public string Name { get; private set; }
    public Guid? ParentTopicId { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Cycle/self-parent/scope validation happens in the
    /// Application handler — it needs database access to walk the parent
    /// chain, which this aggregate doesn't have.</summary>
    public void UpdateDetails(string name, Guid? parentTopicId, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        ParentTopicId = parentTopicId;
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
