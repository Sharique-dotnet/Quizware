using QuizApp.Domain.Common;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Tournament;

/// <summary>Replaces the 3-row hardcoded Rounds table. A stage needs at least
/// one segment (StageSegmentTemplate row) — and that is the only requirement.
/// No format is compulsory.</summary>
public sealed class Stage : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private Stage()
    {
        Name = string.Empty;
        CreatedBy = string.Empty;
    }

    public static Stage Create(
        Guid programId, string name, int orderIndex, StageType stageType, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Stage
        {
            ProgramId = programId,
            Name = name,
            OrderIndex = orderIndex,
            StageType = stageType,
            State = StageState.Draft,
            MinTeamsPerMatch = 2,
            MaxTeamsPerMatch = 3,
            RequiresPreviousStageComplete = true,
            TeamCountChangePolicy = TeamCountChangePolicy.KeepPlanned,
            SegmentOrderMode = SegmentOrderMode.Fixed,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public string Name { get; private set; }
    public string? ShortName { get; private set; }
    public int OrderIndex { get; private set; }
    public StageType StageType { get; private set; }
    public int? PlannedMatchCount { get; private set; }
    public int MinTeamsPerMatch { get; private set; }
    public int MaxTeamsPerMatch { get; private set; }
    public StageState State { get; private set; }
    public bool RequiresPreviousStageComplete { get; private set; }
    public TeamCountChangePolicy TeamCountChangePolicy { get; private set; }
    public SegmentOrderMode SegmentOrderMode { get; private set; }
    public bool AllowSegmentReorderDuringMatch { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>A stage needs at least one segment before it can be marked
    /// Ready. <paramref name="segmentCount"/> is supplied by the caller.</summary>
    public void MarkReady(int segmentCount)
    {
        if (State != StageState.Draft)
        {
            throw new InvalidStateTransitionException($"Stage '{Name}' must be Draft to become Ready; it is {State}.");
        }

        if (segmentCount < 1)
        {
            throw new InvalidStateTransitionException($"Stage '{Name}' needs at least one segment before it can be Ready.");
        }

        State = StageState.Ready;
    }

    public void Rename(string name, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Reorder(int newOrderIndex, string updatedBy)
    {
        OrderIndex = newOrderIndex;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SetSegmentOrderMode(SegmentOrderMode mode, string updatedBy)
    {
        SegmentOrderMode = mode;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Start()
    {
        if (State != StageState.Ready)
        {
            throw new InvalidStateTransitionException($"Stage '{Name}' must be Ready to start; it is {State}.");
        }

        State = StageState.InProgress;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (State != StageState.InProgress)
        {
            throw new InvalidStateTransitionException($"Stage '{Name}' must be InProgress to complete; it is {State}.");
        }

        State = StageState.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Delete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }
}
