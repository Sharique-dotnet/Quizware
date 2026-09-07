using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizApp.Domain.Scoring;

namespace QuizApp.Infrastructure.Persistence.Configurations;

public sealed class ScoringRuleConfiguration : IEntityTypeConfiguration<ScoringRule>
{
    public void Configure(EntityTypeBuilder<ScoringRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ContextKey).HasMaxLength(50);
        builder.HasIndex(r => new { r.ProgramId, r.StageId, r.SegmentTemplateId, r.FormatCode, r.Outcome, r.ContextKey })
            .IsUnique()
            .HasFilter("IsDeleted = 0");
    }
}

public sealed class ScoreEventConfiguration : IEntityTypeConfiguration<ScoreEvent>
{
    public void Configure(EntityTypeBuilder<ScoreEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.MatchId, e.TeamId }).IncludeProperties(e => new { e.Points, e.IsReversed, e.EventType });
        builder.HasIndex(e => new { e.ProgramId, e.TeamId });
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ScoreEvent_ManualReason", "EventType <> 2 OR (Reason IS NOT NULL AND Reason <> '')"));
    }
}

public sealed class TeamMatchScoreConfiguration : IEntityTypeConfiguration<TeamMatchScore>
{
    public void Configure(EntityTypeBuilder<TeamMatchScore> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.MatchId, s.TeamId }).IsUnique();
        builder.HasIndex(s => new { s.MatchId, s.TotalPoints }).IncludeProperties(s => new { s.TeamId, s.Rank });
    }
}

public sealed class TeamStageScoreConfiguration : IEntityTypeConfiguration<TeamStageScore>
{
    public void Configure(EntityTypeBuilder<TeamStageScore> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.StageId, s.TeamId }).IsUnique();
        builder.HasIndex(s => new { s.StageId, s.TotalPoints }).IncludeProperties(s => new { s.TeamId, s.Rank });
    }
}
