namespace QuizApp.Infrastructure.Auditing;

/// <summary>Written automatically by AuditLogSaveChangesInterceptor. Captures
/// old/new values and changed columns for every insert/update/delete.</summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? ProgramId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangedColumns { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
