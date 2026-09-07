using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Idempotency;

namespace QuizApp.Infrastructure.Persistence;

/// <summary>
/// Phase 3 scope only: Identity, refresh tokens, and idempotency records —
/// the cross-cutting concerns this phase needs a working login endpoint and
/// idempotent writes. The full 51-table business schema is added to this
/// same context in Phase 4 (P4-01 onward), not a second context.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(200);
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Key).IsRequired().HasMaxLength(100);
            entity.Property(r => r.RequestBodyHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(r => new { r.ProgramId, r.Key }).IsUnique();
        });
    }
}
