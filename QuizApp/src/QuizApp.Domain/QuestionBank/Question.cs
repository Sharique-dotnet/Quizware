using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

/// <summary>
/// The base table shared by all ten formats (Table-Per-Type). No
/// format-specific column lives here — see the sealed subclasses.
/// There is deliberately no MatchId, StageId or QuestionNumber: questions are
/// never pre-assigned to a match or a position.
/// </summary>
public abstract class Question : BaseEntity, IAuditable, ISoftDeletable
{
    protected Question()
    {
        Language = string.Empty;
        CreatedBy = string.Empty;
    }

    protected Question(
        Guid? programId,
        QuestionOwnerScope ownerScope,
        QuestionFormatCode formatCode,
        string? questionText,
        DifficultyLevel difficultyLevel,
        string language,
        string createdBy,
        Guid? topicId)
    {
        if (ownerScope == QuestionOwnerScope.Program && programId is null)
        {
            throw new ArgumentException("ProgramId is required when OwnerScope is Program.", nameof(programId));
        }

        if (ownerScope == QuestionOwnerScope.Organisation && programId is not null)
        {
            throw new ArgumentException("ProgramId must be null when OwnerScope is Organisation.", nameof(programId));
        }

        ProgramId = programId;
        OwnerScope = ownerScope;
        FormatCode = formatCode;
        QuestionText = questionText;
        DifficultyLevel = difficultyLevel;
        TopicId = topicId;
        Language = language;
        Status = QuestionStatus.Draft;
        Version = 1;
        CreatedAtUtc = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public Guid? ProgramId { get; private set; }
    public QuestionOwnerScope OwnerScope { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public string? QuestionText { get; private set; }
    public string? Explanation { get; private set; }
    public DifficultyLevel DifficultyLevel { get; private set; }
    public Guid? TopicId { get; private set; }
    public string Language { get; private set; }
    public int? TimeLimitSeconds { get; private set; }
    public int? PointsOverride { get; private set; }
    public string? Source { get; private set; }
    public QuestionStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public int TimesUsed { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public string? NormalizedText { get; private set; }
    public int Version { get; private set; }
    public Guid? SupersedesQuestionId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void Approve(Guid approvedBy)
    {
        if (Status != QuestionStatus.Draft)
        {
            throw new InvalidOperationException($"Only a Draft question may be approved; this one is {Status}.");
        }

        Status = QuestionStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAtUtc = DateTime.UtcNow;
    }

    public void Retire()
    {
        Status = QuestionStatus.Retired;
    }

    /// <summary>Only Approved questions are selectable by the draw.</summary>
    public bool IsSelectable => Status == QuestionStatus.Approved && !IsDeleted;

    public void RecordUsage()
    {
        TimesUsed++;
        LastUsedAtUtc = DateTime.UtcNow;
    }
}
