using Quizware.Domain.Common;

namespace Quizware.Domain.Teams;

/// <summary>Certificates, name display, and knowing who actually played.</summary>
public sealed class TeamMember : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private TeamMember()
    {
        FullName = string.Empty;
        CreatedBy = string.Empty;
    }

    public static TeamMember Create(
        Guid programId,
        Guid teamId,
        string fullName,
        string createdBy,
        string? rollNumber = null,
        string? className = null,
        bool isCaptain = false)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("FullName is required.", nameof(fullName));
        }

        return new TeamMember
        {
            ProgramId = programId,
            TeamId = teamId,
            FullName = fullName,
            RollNumber = rollNumber,
            Class = className,
            IsCaptain = isCaptain,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid TeamId { get; private set; }
    public string FullName { get; private set; }
    public string? RollNumber { get; private set; }
    public string? Class { get; private set; }
    public bool IsCaptain { get; private set; }
    public string? PhotoUrl { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
