using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common;

namespace Quizware.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills in audit fields the developer never sets by hand. Domain factory
/// methods already stamp CreatedAtUtc/CreatedBy explicitly (they require a
/// createdBy argument), so this interceptor's real job is UpdatedAtUtc /
/// UpdatedBy on every Modified entity — a safety net, not the only source.
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AuditableEntitySaveChangesInterceptor(IClock clock, ICurrentUser currentUser)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var actor = _currentUser.IsAuthenticated ? _currentUser.Email ?? _currentUser.UserId?.ToString() : null;
        var now = _clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue is DateTime { } created && created == default)
                {
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = now;
                }

                if (entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue is string { Length: 0 } && actor is not null)
                {
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = actor;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAtUtc)).CurrentValue = now;

                if (actor is not null)
                {
                    entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = actor;
                }
            }
        }
    }
}
