using Quizware.Domain.Common;

namespace Quizware.Domain.QuestionBank;

/// <summary>Flexible filtering without schema changes. ProgramId null means shared.</summary>
public sealed class Tag : BaseEntity, IAuditable, ISoftDeletable
{
    private Tag()
    {
        Name = string.Empty;
        CreatedBy = string.Empty;
    }

    public static Tag Create(string name, string createdBy, Guid? programId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Tag
        {
            ProgramId = programId,
            Name = name,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid? ProgramId { get; private set; }
    public string Name { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void UpdateDetails(string name, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
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
