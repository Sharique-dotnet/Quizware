using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Teams;

/// <summary>Replaces SchoolsTeam. Scoped to a program; no hardcoded team limit.</summary>
public sealed class Team : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private Team()
    {
        Code = string.Empty;
        SchoolName = string.Empty;
        DisplayName = string.Empty;
        CreatedBy = string.Empty;
    }

    public static Team Register(
        Guid programId,
        string code,
        string schoolName,
        string displayName,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(schoolName))
        {
            throw new ArgumentException("SchoolName is required.", nameof(schoolName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(displayName));
        }

        return new Team
        {
            ProgramId = programId,
            Code = code,
            SchoolName = schoolName,
            DisplayName = displayName,
            Status = TeamStatus.Registered,
            RegisteredAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public string Code { get; private set; }
    public string SchoolName { get; private set; }
    public string DisplayName { get; private set; }
    public string? ShortName { get; private set; }
    public string? ScoreImageUrl { get; private set; }
    public string? SelectionImageUrl { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public TeamStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public DateTime? StatusChangedAtUtc { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public int SortOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Every status change requires a reason — there is no silent transition.</summary>
    public void ChangeStatus(TeamStatus newStatus, string reason, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to change a team's status.", nameof(reason));
        }

        Status = newStatus;
        StatusReason = reason;
        StatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateDetails(
        string schoolName,
        string displayName,
        string? shortName,
        string? contactName,
        string? contactPhone,
        string? contactEmail,
        string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(schoolName))
        {
            throw new ArgumentException("SchoolName is required.", nameof(schoolName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(displayName));
        }

        SchoolName = schoolName;
        DisplayName = displayName;
        ShortName = shortName;
        ContactName = contactName;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SetImages(string? scoreImageUrl, string? selectionImageUrl, string updatedBy)
    {
        ScoreImageUrl = scoreImageUrl;
        SelectionImageUrl = selectionImageUrl;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Soft delete. Callers must first confirm the team was never
    /// used in a match (FR-2.6) — that check needs match data this
    /// aggregate doesn't have, so it lives in the caller, not here.</summary>
    public void Delete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }
}
