using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Tournament;

/// <summary>How questions are drawn. Most specific wins: segment rule, then
/// stage rule, then program-wide rule.</summary>
public sealed class QuestionSelectionRule : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private QuestionSelectionRule()
    {
        CreatedBy = string.Empty;
    }

    public static QuestionSelectionRule Create(
        Guid programId, QuestionFormatCode formatCode, string createdBy,
        Guid? stageId = null, Guid? segmentTemplateId = null,
        DifficultyLevel minDifficultyLevel = DifficultyLevel.VeryEasy,
        DifficultyLevel maxDifficultyLevel = DifficultyLevel.VeryHard)
    {
        return new QuestionSelectionRule
        {
            ProgramId = programId,
            StageId = stageId,
            SegmentTemplateId = segmentTemplateId,
            FormatCode = formatCode,
            MinDifficultyLevel = minDifficultyLevel,
            MaxDifficultyLevel = maxDifficultyLevel,
            RepeatPolicy = RepeatPolicy.NeverInProgram,
            TopicSpreadPolicy = TopicSpreadPolicy.None,
            ShuffleOptions = true,
            FallbackPolicy = FallbackPolicy.WidenThenFail,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid? StageId { get; private set; }
    public Guid? SegmentTemplateId { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public DifficultyLevel MinDifficultyLevel { get; private set; }
    public DifficultyLevel MaxDifficultyLevel { get; private set; }
    public string? DifficultyMixJson { get; private set; }
    public string? TopicFilterJson { get; private set; }
    public string? TagFilterJson { get; private set; }
    public string? Language { get; private set; }
    public RepeatPolicy RepeatPolicy { get; private set; }
    public TopicSpreadPolicy TopicSpreadPolicy { get; private set; }
    public bool ShuffleOptions { get; private set; }
    public bool UseSharedLibrary { get; private set; }
    public FallbackPolicy FallbackPolicy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}
