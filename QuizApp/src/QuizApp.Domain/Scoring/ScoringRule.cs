using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Scoring;

/// <summary>Replaces Contants.cs. Lookup order is segment rule -> stage rule
/// -> program rule -> ScoringRuleNotFoundException (never guess).</summary>
public sealed class ScoringRule : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private ScoringRule()
    {
        CreatedBy = string.Empty;
    }

    public static ScoringRule Create(
        Guid programId, QuestionFormatCode formatCode, AnswerOutcome outcome, int points, string createdBy,
        Guid? stageId = null, Guid? segmentTemplateId = null, string? contextKey = null,
        DifficultyLevel? appliesToDifficultyLevel = null, string? description = null)
    {
        return new ScoringRule
        {
            ProgramId = programId,
            StageId = stageId,
            SegmentTemplateId = segmentTemplateId,
            FormatCode = formatCode,
            Outcome = outcome,
            ContextKey = contextKey,
            Points = points,
            AppliesToDifficultyLevel = appliesToDifficultyLevel,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid? StageId { get; private set; }
    public Guid? SegmentTemplateId { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public AnswerOutcome Outcome { get; private set; }
    public string? ContextKey { get; private set; }
    public int Points { get; private set; }
    public DifficultyLevel? AppliesToDifficultyLevel { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Specificity: a segment-level rule beats a stage-level rule,
    /// which beats a program-level rule.</summary>
    public int Specificity => SegmentTemplateId is not null ? 2 : StageId is not null ? 1 : 0;
}
