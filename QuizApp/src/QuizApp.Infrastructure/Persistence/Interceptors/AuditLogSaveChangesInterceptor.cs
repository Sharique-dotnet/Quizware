using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizApp.Application.Abstractions;
using QuizApp.Infrastructure.Auditing;

namespace QuizApp.Infrastructure.Persistence.Interceptors;

/// <summary>Writes an AuditLog row for every insert/update/delete, capturing
/// old/new values and which columns changed — automatically, from a
/// SaveChanges interceptor, never from application code.</summary>
public sealed class AuditLogSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentProgram _currentProgram;

    public AuditLogSaveChangesInterceptor(ICurrentUser currentUser, ICurrentProgram currentProgram)
    {
        _currentUser = currentUser;
        _currentProgram = currentProgram;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteAuditRows(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        WriteAuditRows(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var rows = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null,
            };

            if (action is null)
            {
                continue;
            }

            var oldValues = action != "Insert" ? Serialize(entry, useOriginalValues: true) : null;
            var newValues = action != "Delete" ? Serialize(entry, useOriginalValues: false) : null;
            var changedColumns = action == "Update"
                ? string.Join(",", entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))
                : null;

            rows.Add(new AuditLog
            {
                ProgramId = TryGetGuidProperty(entry, "ProgramId") ?? (_currentProgram.HasProgram ? _currentProgram.ProgramId : null),
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = TryGetGuidProperty(entry, "Id")?.ToString() ?? "unknown",
                Action = action,
                OldValuesJson = oldValues,
                NewValuesJson = newValues,
                ChangedColumns = changedColumns,
                UserId = _currentUser.UserId,
                UserName = _currentUser.Email,
                OccurredAtUtc = DateTime.UtcNow,
            });
        }

        if (rows.Count > 0)
        {
            context.Set<AuditLog>().AddRange(rows);
        }
    }

    private static Guid? TryGetGuidProperty(EntityEntry entry, string propertyName)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
        return property?.CurrentValue is Guid guid ? guid : null;
    }

    private static string Serialize(EntityEntry entry, bool useOriginalValues)
    {
        var values = entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => useOriginalValues ? p.OriginalValue : p.CurrentValue);

        return JsonSerializer.Serialize(values);
    }
}
