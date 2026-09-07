namespace QuizApp.Infrastructure.Identity;

/// <summary>Which user may work on which program, and with which role
/// there — a user can be ProgramAdmin on one program and Operator on
/// another. This is what makes the JWT program claim safe.</summary>
public sealed class ProgramUser
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
