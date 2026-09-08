using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table-Per-Type (ADR-003): Question is the shared base table; each format
/// gets its own table via UseTptMappingStrategy, joined 1:1 on QuestionId.
/// </summary>
public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.UseTptMappingStrategy();
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Language).HasMaxLength(10).IsRequired();
        builder.Property(q => q.NormalizedText).HasMaxLength(450);

        builder.HasIndex(q => new { q.ProgramId, q.FormatCode, q.DifficultyLevel, q.Status })
            .IncludeProperties(q => new { q.TopicId, q.Language, q.TimesUsed });
        builder.HasIndex(q => q.TopicId).HasFilter("IsDeleted = 0");
        builder.HasIndex(q => new { q.ProgramId, q.NormalizedText });
        builder.HasIndex(q => new { q.ProgramId, q.FormatCode, q.TimesUsed });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Question_OwnerScope",
                "(OwnerScope = 1 AND ProgramId IS NOT NULL) OR (OwnerScope = 2 AND ProgramId IS NULL)");
            // Documented in 04-Database-Schema.md but was missing from this
            // config entirely.
            t.HasCheckConstraint("CK_Question_Difficulty", "DifficultyLevel BETWEEN 1 AND 5");
        });
    }
}

public sealed class McqQuestionConfiguration : IEntityTypeConfiguration<McqQuestion>
{
    public void Configure(EntityTypeBuilder<McqQuestion> builder) => builder.ToTable("McqQuestion");
}

public sealed class BuzzerQuestionConfiguration : IEntityTypeConfiguration<BuzzerQuestion>
{
    public void Configure(EntityTypeBuilder<BuzzerQuestion> builder) => builder.ToTable("BuzzerQuestion");
}

public sealed class PassingQuestionConfiguration : IEntityTypeConfiguration<PassingQuestion>
{
    public void Configure(EntityTypeBuilder<PassingQuestion> builder) => builder.ToTable("PassingQuestion");
}

public sealed class CardQuestionConfiguration : IEntityTypeConfiguration<CardQuestion>
{
    public void Configure(EntityTypeBuilder<CardQuestion> builder) => builder.ToTable("CardQuestion");
}

public sealed class ChoiceQuestionConfiguration : IEntityTypeConfiguration<ChoiceQuestion>
{
    public void Configure(EntityTypeBuilder<ChoiceQuestion> builder)
    {
        builder.ToTable("ChoiceQuestion");
        builder.Property(q => q.TopicLabel).HasMaxLength(150).IsRequired();
    }
}

public sealed class SequenceQuestionConfiguration : IEntityTypeConfiguration<SequenceQuestion>
{
    public void Configure(EntityTypeBuilder<SequenceQuestion> builder) => builder.ToTable("SequenceQuestion");
}

public sealed class AudioVisualQuestionConfiguration : IEntityTypeConfiguration<AudioVisualQuestion>
{
    public void Configure(EntityTypeBuilder<AudioVisualQuestion> builder)
    {
        builder.ToTable("AudioVisualQuestion");
        builder.Property(q => q.AnswerText).IsRequired();
    }
}

public sealed class RapidFireQuestionConfiguration : IEntityTypeConfiguration<RapidFireQuestion>
{
    public void Configure(EntityTypeBuilder<RapidFireQuestion> builder) => builder.ToTable("RapidFireQuestion");
}

public sealed class VisualRapidFireQuestionConfiguration : IEntityTypeConfiguration<VisualRapidFireQuestion>
{
    public void Configure(EntityTypeBuilder<VisualRapidFireQuestion> builder) => builder.ToTable("VisualRapidFireQuestion");
}

public sealed class TieBreakerQuestionConfiguration : IEntityTypeConfiguration<TieBreakerQuestion>
{
    public void Configure(EntityTypeBuilder<TieBreakerQuestion> builder)
    {
        builder.ToTable("TieBreakerQuestion");
        builder.Property(q => q.NumericAnswer).HasPrecision(18, 4);
    }
}

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => new { o.QuestionId, o.DisplayOrder });
    }
}

public sealed class SequenceItemConfiguration : IEntityTypeConfiguration<SequenceItem>
{
    public void Configure(EntityTypeBuilder<SequenceItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => new { i.QuestionId, i.CorrectPosition }).IsUnique();
        builder.HasIndex(i => new { i.QuestionId, i.DisplayOrder });
    }
}

public sealed class VisualRapidFireItemConfiguration : IEntityTypeConfiguration<VisualRapidFireItem>
{
    public void Configure(EntityTypeBuilder<VisualRapidFireItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => new { i.QuestionId, i.DisplayOrder });
    }
}

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(t => new { t.ProgramId, t.Name }).IsUnique().HasFilter("IsDeleted = 0");
        builder.HasIndex(t => t.ParentTopicId);
        // Self FK (04-Database-Schema.md calls ParentTopicId one), no
        // navigation property needed. Restrict, not cascade — deleting a
        // topic must never silently orphan/cascade a subtree; the
        // Application-layer delete guard is what actually prevents that.
        builder.HasOne<Topic>().WithMany().HasForeignKey(t => t.ParentTopicId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(t => new { t.ProgramId, t.Name }).IsUnique().HasFilter("IsDeleted = 0");
    }
}

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FileName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(m => m.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ChecksumSha256).HasMaxLength(32).IsRequired();
        builder.HasIndex(m => m.ChecksumSha256);
    }
}

public sealed class QuestionUsageHistoryConfiguration : IEntityTypeConfiguration<QuestionUsageHistory>
{
    public void Configure(EntityTypeBuilder<QuestionUsageHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.HasIndex(h => new { h.ProgramId, h.QuestionId });
    }
}
