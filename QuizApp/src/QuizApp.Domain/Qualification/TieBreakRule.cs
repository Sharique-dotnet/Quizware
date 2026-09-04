using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Qualification;

/// <summary>How a tie is settled. Criteria are ordered and tried first,
/// cheapest checks first; only if none separates the teams does a tie-break
/// segment get played (MCQ by default, any format is configurable).</summary>
public sealed class TieBreakRule : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private TieBreakRule()
    {
        Name = string.Empty;
        Criteria = Array.Empty<string>();
        CreatedBy = string.Empty;
    }

    public static TieBreakRule Create(
        Guid programId, string name, TieBreakScope scope, IReadOnlyList<string> criteria, string createdBy,
        Guid? stageId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (criteria.Count == 0)
        {
            throw new ArgumentException("At least one ordered criterion is required.", nameof(criteria));
        }

        return new TieBreakRule
        {
            ProgramId = programId,
            StageId = stageId,
            Name = name,
            Scope = scope,
            Criteria = criteria,
            PlayTieBreakSegment = true,
            TieBreakFormatCode = QuestionFormatCode.Mcq,
            QuestionCount = 3,
            MinDifficultyLevel = DifficultyLevel.Medium,
            MaxDifficultyLevel = DifficultyLevel.VeryHard,
            MaxExtraRounds = 3,
            OnStillTied = OnStillTiedPolicy.ManualDecision,
            RequiresAdminApproval = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid? StageId { get; private set; }
    public string Name { get; private set; }
    public TieBreakScope Scope { get; private set; }
    public IReadOnlyList<string> Criteria { get; private set; }
    public bool PlayTieBreakSegment { get; private set; }
    public QuestionFormatCode TieBreakFormatCode { get; private set; }
    public int QuestionCount { get; private set; }
    public DifficultyLevel MinDifficultyLevel { get; private set; }
    public DifficultyLevel MaxDifficultyLevel { get; private set; }
    public int? TimeLimitSeconds { get; private set; }
    public bool SuddenDeath { get; private set; }
    public int MaxExtraRounds { get; private set; }
    public OnStillTiedPolicy OnStillTied { get; private set; }
    public bool ScoreCountsTowardStage { get; private set; }
    public bool RequiresAdminApproval { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
