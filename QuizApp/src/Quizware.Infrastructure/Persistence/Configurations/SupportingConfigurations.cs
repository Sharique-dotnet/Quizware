using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quizware.Infrastructure.Auditing;
using Quizware.Infrastructure.Imports;
using Quizware.Infrastructure.Outbox;

namespace Quizware.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ChangedColumns).HasMaxLength(1000);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.OccurredAtUtc);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.HasIndex(m => m.ProcessedAtUtc).HasFilter("ProcessedAtUtc IS NULL");
    }
}

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.ImportType).HasMaxLength(50).IsRequired();
        builder.Property(b => b.FileName).HasMaxLength(255).IsRequired();
        builder.HasIndex(b => b.ProgramId);
    }
}

public sealed class ImportBatchRowConfiguration : IEntityTypeConfiguration<ImportBatchRow>
{
    public void Configure(EntityTypeBuilder<ImportBatchRow> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.ImportBatchId);
    }
}
