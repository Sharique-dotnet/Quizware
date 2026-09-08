using Microsoft.EntityFrameworkCore;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Programs;
using Quizware.Domain.Qualification;
using Quizware.Domain.QuestionBank;
using Quizware.Domain.Scoring;
using Quizware.Domain.Teams;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Abstractions;

/// <summary>
/// The persistence port every Application-layer handler queries and writes
/// through (02-Architecture-Proposal.md line 94). Exposes every
/// Domain-owned entity set up front so later phases never need to reopen
/// this interface — only the concrete <c>AppDbContext</c> in Infrastructure
/// implements it. Identity/Infrastructure-only tables (RefreshToken,
/// ProgramUser, IdempotencyRecord, AuditLog, OutboxMessage, ImportBatch)
/// are deliberately excluded: their entity types live in Infrastructure,
/// which Application must never reference.
/// </summary>
public interface IAppDbContext
{
    DbSet<Program> Programs { get; }
    DbSet<ProgramSetting> ProgramSettings { get; }
    DbSet<ProgramQuestionFormat> ProgramQuestionFormats { get; }

    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }

    DbSet<Topic> Topics { get; }
    DbSet<Tag> Tags { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<SequenceItem> SequenceItems { get; }
    DbSet<VisualRapidFireItem> VisualRapidFireItems { get; }
    DbSet<QuestionUsageHistory> QuestionUsageHistories { get; }

    DbSet<Stage> Stages { get; }
    DbSet<StageSegmentTemplate> StageSegmentTemplates { get; }
    DbSet<QuestionSelectionRule> QuestionSelectionRules { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchParticipant> MatchParticipants { get; }

    DbSet<MatchSegment> MatchSegments { get; }
    DbSet<MatchQuestion> MatchQuestions { get; }
    DbSet<AnswerRecord> AnswerRecords { get; }
    DbSet<MatchEvent> MatchEvents { get; }

    DbSet<ScoringRule> ScoringRules { get; }
    DbSet<ScoreEvent> ScoreEvents { get; }
    DbSet<TeamMatchScore> TeamMatchScores { get; }
    DbSet<TeamStageScore> TeamStageScores { get; }

    DbSet<QualificationRule> QualificationRules { get; }
    DbSet<StageQualification> StageQualifications { get; }
    DbSet<TieBreakRule> TieBreakRules { get; }
    DbSet<TieBreakEvent> TieBreakEvents { get; }
    DbSet<TieBreakParticipant> TieBreakParticipants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
