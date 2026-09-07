using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizApp.Domain.Teams;

namespace QuizApp.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(30).IsRequired();
        builder.Property(t => t.SchoolName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => new { t.ProgramId, t.Code }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(t => new { t.ProgramId, t.Status });
    }
}

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FullName).HasMaxLength(200).IsRequired();
        builder.HasIndex(m => m.TeamId);
    }
}
