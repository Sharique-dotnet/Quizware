using Quizware.Domain.Common;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Gameplay;

/// <summary>A live instance of a segment template — the state machine of the
/// live match. On restart, the engine finds the one Open segment and resumes
/// exactly there.</summary>
public sealed class MatchSegment : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private MatchSegment()
    {
        CreatedBy = string.Empty;
    }

    public static MatchSegment Create(
        Guid programId, Guid matchId, Guid segmentTemplateId, QuestionFormatCode formatCode,
        int orderIndex, int plannedQuestionCount, string createdBy)
    {
        return new MatchSegment
        {
            ProgramId = programId,
            MatchId = matchId,
            SegmentTemplateId = segmentTemplateId,
            FormatCode = formatCode,
            OrderIndex = orderIndex,
            PlannedQuestionCount = plannedQuestionCount,
            State = MatchSegmentState.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid SegmentTemplateId { get; private set; }
    public QuestionFormatCode FormatCode { get; private set; }
    public int OrderIndex { get; private set; }
    public int PlannedQuestionCount { get; private set; }
    public int ServedQuestionCount { get; private set; }
    public bool IsSuddenDeath { get; private set; }
    public bool IsOrderLocked { get; private set; }
    public MatchSegmentState State { get; private set; }
    public string? SkipReason { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Only one segment may be Open per match — the caller passes
    /// every other segment belonging to the same match so this can be checked.</summary>
    public void Open(IReadOnlyList<MatchSegment> otherSegmentsInMatch)
    {
        if (State != MatchSegmentState.Pending)
        {
            throw new InvalidStateTransitionException($"Segment {OrderIndex} must be Pending to open; it is {State}.");
        }

        if (otherSegmentsInMatch.Any(s => s.Id != Id && s.MatchId == MatchId && s.State == MatchSegmentState.Open))
        {
            throw new InvalidStateTransitionException(
                $"Match already has an open segment; segment {OrderIndex} cannot also be opened.");
        }

        State = MatchSegmentState.Open;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void RecordQuestionServed()
    {
        ServedQuestionCount++;
    }

    public void Lock()
    {
        IsOrderLocked = true;
    }

    /// <summary>Live reorder of a pending segment. Refuses once the segment
    /// has been opened or completed, and refuses a locked segment — matching
    /// SEGMENT_NOT_REORDERABLE.</summary>
    public void Reorder(int newOrderIndex)
    {
        if (IsOrderLocked)
        {
            throw new SegmentNotReorderableException($"Segment {OrderIndex} is locked and cannot be reordered.");
        }

        if (State != MatchSegmentState.Pending)
        {
            throw new SegmentNotReorderableException(
                $"Segment {OrderIndex} is {State}; only a Pending segment may be reordered.");
        }

        OrderIndex = newOrderIndex;
    }

    public void Complete()
    {
        if (State != MatchSegmentState.Open)
        {
            throw new InvalidStateTransitionException($"Segment {OrderIndex} must be Open to complete; it is {State}.");
        }

        State = MatchSegmentState.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Skip(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to skip a segment.", nameof(reason));
        }

        State = MatchSegmentState.Skipped;
        SkipReason = reason;
    }
}
