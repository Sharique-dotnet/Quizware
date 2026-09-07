using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizApp.Domain.Tournament;

namespace QuizApp.Infrastructure.Persistence.Configurations;

public sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(s => new { s.ProgramId, s.OrderIndex }).IsUnique().HasFilter("IsDeleted = 0");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Stage_TeamRange", "MinTeamsPerMatch >= 2 AND MaxTeamsPerMatch >= MinTeamsPerMatch"));
    }
}

public sealed class StageSegmentTemplateConfiguration : IEntityTypeConfiguration<StageSegmentTemplate>
{
    public void Configure(EntityTypeBuilder<StageSegmentTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => new { t.StageId, t.OrderIndex }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.StageId, m.MatchNumber }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(m => new { m.ProgramId, m.State });
        builder.HasIndex(m => new { m.StageId, m.State });
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Match_TieBreak",
            "(MatchKind = 1 AND TieBreakEventId IS NULL) OR (MatchKind = 2 AND TieBreakEventId IS NOT NULL)"));
    }
}

public sealed class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.MatchId, p.TeamId }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(p => new { p.MatchId, p.SeatNumber }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(p => new { p.MatchId, p.Status, p.TurnOrder })
            .IncludeProperties(p => new { p.TeamId, p.SeatNumber })
            .HasFilter("IsDeleted = 0");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_MP_Removal", "Status = 1 OR (RemovalReason IS NOT NULL AND RemovedAtUtc IS NOT NULL)"));
    }
}

public sealed class QuestionSelectionRuleConfiguration : IEntityTypeConfiguration<QuestionSelectionRule>
{
    public void Configure(EntityTypeBuilder<QuestionSelectionRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.ProgramId, r.StageId, r.FormatCode });
    }
}
