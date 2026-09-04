using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Programs;

/// <summary>
/// Declares, in one place, which of the ten question formats a program uses.
/// No format is compulsory. Disabling one here is a convenience and a safety
/// net for the admin UI — it is not what decides selectability (that is the
/// question's own Approved state plus the pool filters).
/// </summary>
public sealed class ProgramQuestionFormat : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private ProgramQuestionFormat()
    {
        CreatedBy = string.Empty;
    }

    public static ProgramQuestionFormat Create(
        Guid programId,
        QuestionFormatCode formatCode,
        string createdBy,
        int displayOrder = 0)
    {
        return new ProgramQuestionFormat
        {
            ProgramId = programId,
            FormatCode = formatCode,
            IsEnabled = true,
            DisplayOrder = displayOrder,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public bool IsEnabled { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? DisabledReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void Disable(string reason, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to disable a question format.", nameof(reason));
        }

        IsEnabled = false;
        DisabledReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Enable(string updatedBy)
    {
        IsEnabled = true;
        DisabledReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
