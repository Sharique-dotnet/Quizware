using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Programs;
using Quizware.Domain.Qualification;
using Quizware.Domain.QuestionBank;
using Quizware.Domain.Scoring;
using Quizware.Domain.Teams;
using Quizware.Domain.Tournament;
using Quizware.Infrastructure.Auditing;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Idempotency;
using Quizware.Infrastructure.Imports;
using Quizware.Infrastructure.Outbox;

namespace Quizware.Infrastructure.Persistence;

/// <summary>
/// The one DbContext for the whole system (ADR-002/§4.16). Phase 3 added
/// Identity, RefreshToken and IdempotencyRecord; Phase 4 (this) adds the
/// full 51-table business schema on top of the same context.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IAppDbContext
{
    private readonly ICurrentProgram _currentProgram;
    private readonly IEnumerable<IEntityConfigurationAssemblyMarker> _extraConfigurationSources;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentProgram currentProgram,
        IEnumerable<IEntityConfigurationAssemblyMarker> extraConfigurationSources)
        : base(options)
    {
        _currentProgram = currentProgram;
        _extraConfigurationSources = extraConfigurationSources;
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProgramUser> ProgramUsers => Set<ProgramUser>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportBatchRow> ImportBatchRows => Set<ImportBatchRow>();

    public DbSet<Program> Programs => Set<Program>();
    public DbSet<ProgramSetting> ProgramSettings => Set<ProgramSetting>();
    public DbSet<ProgramQuestionFormat> ProgramQuestionFormats => Set<ProgramQuestionFormat>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<SequenceItem> SequenceItems => Set<SequenceItem>();
    public DbSet<VisualRapidFireItem> VisualRapidFireItems => Set<VisualRapidFireItem>();
    public DbSet<QuestionUsageHistory> QuestionUsageHistories => Set<QuestionUsageHistory>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<StageSegmentTemplate> StageSegmentTemplates => Set<StageSegmentTemplate>();
    public DbSet<QuestionSelectionRule> QuestionSelectionRules => Set<QuestionSelectionRule>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<MatchSegment> MatchSegments => Set<MatchSegment>();
    public DbSet<MatchQuestion> MatchQuestions => Set<MatchQuestion>();
    public DbSet<AnswerRecord> AnswerRecords => Set<AnswerRecord>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<ScoringRule> ScoringRules => Set<ScoringRule>();
    public DbSet<ScoreEvent> ScoreEvents => Set<ScoreEvent>();
    public DbSet<TeamMatchScore> TeamMatchScores => Set<TeamMatchScore>();
    public DbSet<TeamStageScore> TeamStageScores => Set<TeamStageScore>();
    public DbSet<QualificationRule> QualificationRules => Set<QualificationRule>();
    public DbSet<StageQualification> StageQualifications => Set<StageQualification>();
    public DbSet<TieBreakRule> TieBreakRules => Set<TieBreakRule>();
    public DbSet<TieBreakEvent> TieBreakEvents => Set<TieBreakEvent>();
    public DbSet<TieBreakParticipant> TieBreakParticipants => Set<TieBreakParticipant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ADR-005/010: an optional module (e.g. the buzzer module) contributes
        // its own configurations without Infrastructure ever referencing it.
        foreach (var source in _extraConfigurationSources)
        {
            builder.ApplyConfigurationsFromAssembly(source.Assembly);
        }

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

        ApplyGlobalQueryFilters(builder);
    }

    /// <summary>
    /// Tenant + soft-delete filters (P4-15), applied once to the root of
    /// each inheritance hierarchy so they propagate to every derived type
    /// (e.g. all 10 Question format tables inherit Question's filter).
    /// Program scoping degrades to "no filter" outside a program-scoped
    /// request (background jobs, seeding, SuperAdmin cross-program tools)
    /// rather than throwing — see ICurrentProgram.HasProgram.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder builder)
    {
        var applyFilterMethod = typeof(AppDbContext)
            .GetMethod(nameof(BuildAndApplyFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is not null)
            {
                continue; // Filters on a TPT/TPH root apply to derived types automatically.
            }

            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable)
            {
                continue;
            }

            applyFilterMethod.MakeGenericMethod(clrType).Invoke(this, [builder, isTenantScoped, isSoftDeletable]);
        }
    }

    private void BuildAndApplyFilter<TEntity>(ModelBuilder builder, bool isTenantScoped, bool isSoftDeletable)
        where TEntity : class
    {
        System.Linq.Expressions.Expression<Func<TEntity, bool>>? filter = null;

        if (isSoftDeletable)
        {
            System.Linq.Expressions.Expression<Func<TEntity, bool>> softDeleteFilter =
                e => !((ISoftDeletable)e).IsDeleted;
            filter = softDeleteFilter;
        }

        if (isTenantScoped)
        {
            System.Linq.Expressions.Expression<Func<TEntity, bool>> tenantFilter =
                e => !_currentProgram.HasProgram || ((ITenantScoped)e).ProgramId == _currentProgram.ProgramId;
            filter = filter is null ? tenantFilter : GlobalFilterExpressionCombiner.And(filter, tenantFilter);
        }

        builder.Entity<TEntity>().HasQueryFilter(filter!);
    }
}
