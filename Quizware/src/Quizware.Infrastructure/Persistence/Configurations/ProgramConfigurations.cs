using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quizware.Domain.Programs;

namespace Quizware.Infrastructure.Persistence.Configurations;

public sealed class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(p => new { p.State, p.SeasonYear });
    }
}

public sealed class ProgramSettingConfiguration : IEntityTypeConfiguration<ProgramSetting>
{
    public void Configure(EntityTypeBuilder<ProgramSetting> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Category).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.ProgramId, s.Category, s.Key }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class ProgramQuestionFormatConfiguration : IEntityTypeConfiguration<ProgramQuestionFormat>
{
    public void Configure(EntityTypeBuilder<ProgramQuestionFormat> builder)
    {
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => new { f.ProgramId, f.FormatCode }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(f => new { f.ProgramId, f.IsEnabled });
    }
}
