using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizApp.Domain.Gameplay;

namespace QuizApp.Infrastructure.Persistence.Configurations;

public sealed class MatchSegmentConfiguration : IEntityTypeConfiguration<MatchSegment>
{
    public void Configure(EntityTypeBuilder<MatchSegment> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.MatchId, s.OrderIndex }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class MatchQuestionConfiguration : IEntityTypeConfiguration<MatchQuestion>
{
    public void Configure(EntityTypeBuilder<MatchQuestion> builder)
    {
        builder.HasKey(q => q.Id);
        builder.HasIndex(q => new { q.MatchSegmentId, q.OrderIndex }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(q => new { q.MatchId, q.QuestionId }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(q => new { q.MatchId, q.State, q.OrderIndex })
            .IncludeProperties(q => new { q.QuestionId, q.TargetTeamId, q.MatchSegmentId });
        builder.HasIndex(q => q.QuestionId);
    }
}

public sealed class AnswerRecordConfiguration : IEntityTypeConfiguration<AnswerRecord>
{
    public void Configure(EntityTypeBuilder<AnswerRecord> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.ProgramId, a.IdempotencyKey }).IsUnique().HasFilter("IdempotencyKey IS NOT NULL");
        builder.HasIndex(a => new { a.MatchId, a.TeamId });
        builder.HasIndex(a => a.MatchQuestionId);
        builder.HasIndex(a => a.MatchSegmentId);
    }
}

public sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(60).IsRequired();
        builder.HasIndex(e => new { e.MatchId, e.SequenceNumber }).IsUnique();
    }
}
