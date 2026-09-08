using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quizware.Domain.Qualification;

namespace Quizware.Infrastructure.Persistence.Configurations;

public sealed class QualificationRuleConfiguration : IEntityTypeConfiguration<QualificationRule>
{
    public void Configure(EntityTypeBuilder<QualificationRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.ProgramId, r.FromStageId }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class StageQualificationConfiguration : IEntityTypeConfiguration<StageQualification>
{
    public void Configure(EntityTypeBuilder<StageQualification> builder)
    {
        builder.HasKey(q => q.Id);
        builder.HasIndex(q => new { q.FromStageId, q.TeamId });
    }
}

public sealed class TieBreakRuleConfiguration : IEntityTypeConfiguration<TieBreakRule>
{
    public void Configure(EntityTypeBuilder<TieBreakRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(150).IsRequired();
        builder.Property(r => r.Criteria)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Length == 0 ? Array.Empty<string>() : v.Split(',', StringSplitOptions.None))
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                v => v.ToList()));
        builder.HasIndex(r => new { r.ProgramId, r.StageId, r.Scope }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class TieBreakEventConfiguration : IEntityTypeConfiguration<TieBreakEvent>
{
    public void Configure(EntityTypeBuilder<TieBreakEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.StageId, e.State });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_TieBreakEvent_Resolved",
                "State <> 4 OR (ResolutionMethod IS NOT NULL AND ResolvedAtUtc IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_TieBreakEvent_ManualNotes",
                "ResolutionMethod <> 3 OR (Notes IS NOT NULL AND Notes <> '')");
        });
    }
}

public sealed class TieBreakParticipantConfiguration : IEntityTypeConfiguration<TieBreakParticipant>
{
    public void Configure(EntityTypeBuilder<TieBreakParticipant> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.TieBreakEventId, p.TeamId }).IsUnique();
    }
}
