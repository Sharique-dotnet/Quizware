namespace Quizware.Application.Abstractions;

public sealed record AuditLogFilter(
    Guid ProgramId,
    string? EntityName,
    string? EntityId,
    string? Action,
    string? Actor,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public sealed record AuditLogEntry(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    Guid? UserId,
    string? UserName,
    string? ChangedColumns,
    string? OldValuesJson,
    string? NewValuesJson,
    string? CorrelationId,
    DateTime OccurredAtUtc);

public sealed record AuditLogPage(int TotalCount, int Page, int PageSize, IReadOnlyList<AuditLogEntry> Entries);

/// <summary>P13-05: reads the audit log. AuditLog is an Infrastructure-only
/// table (written by the SaveChanges interceptor, never by a handler), so the
/// Application layer queries it through this port. Read-only — audit rows are
/// immutable (FR-11.3).</summary>
public interface IAuditLogReader
{
    Task<AuditLogPage> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken);
}
