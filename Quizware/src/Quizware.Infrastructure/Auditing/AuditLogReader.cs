using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Infrastructure.Auditing;

/// <summary>Queries AuditLog for one program. Always filtered by ProgramId
/// explicitly — the table has no tenant query filter of its own.</summary>
public sealed class AuditLogReader : IAuditLogReader
{
    private readonly AppDbContext _db;

    public AuditLogReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.AsNoTracking().Where(a => a.ProgramId == filter.ProgramId);

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(a => a.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            var actor = filter.Actor.Trim();
            query = Guid.TryParse(actor, out var userId)
                ? query.Where(a => a.UserId == userId)
                : query.Where(a => a.UserName != null && a.UserName.Contains(actor));
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(a => a.OccurredAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(a => a.OccurredAtUtc <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AuditLogEntry(
                a.Id, a.EntityName, a.EntityId, a.Action, a.UserId, a.UserName, a.ChangedColumns,
                a.OldValuesJson, a.NewValuesJson, a.CorrelationId, a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new AuditLogPage(total, filter.Page, filter.PageSize, entries);
    }
}
