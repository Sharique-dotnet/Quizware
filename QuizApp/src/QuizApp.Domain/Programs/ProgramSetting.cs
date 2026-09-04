using QuizApp.Domain.Common;

namespace QuizApp.Domain.Programs;

/// <summary>
/// The escape hatch: new tunable behaviour never needs a migration. Unique on
/// (ProgramId, Category, Key).
/// </summary>
public sealed class ProgramSetting : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private ProgramSetting()
    {
        Category = string.Empty;
        Key = string.Empty;
        Value = string.Empty;
        ValueType = string.Empty;
        CreatedBy = string.Empty;
    }

    public static ProgramSetting Create(
        Guid programId,
        string category,
        string key,
        string value,
        string valueType,
        string createdBy,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        return new ProgramSetting
        {
            ProgramId = programId,
            Category = category,
            Key = key,
            Value = value,
            ValueType = valueType,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public string Category { get; private set; }
    public string Key { get; private set; }
    public string Value { get; private set; }
    public string ValueType { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void UpdateValue(string value, string updatedBy)
    {
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
