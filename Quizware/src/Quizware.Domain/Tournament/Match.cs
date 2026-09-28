using Quizware.Domain.Common;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Tournament;

/// <summary>A tie-break is just a match: MatchKind = TieBreak runs through the
/// same match engine, on the same console, with the same scoring and audit —
/// no separate tie-break gameplay code exists anywhere.</summary>
public sealed class Match : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private Match()
    {
        CreatedBy = string.Empty;
    }

    public static Match Create(
        Guid programId, Guid stageId, int matchNumber, long randomSeed, string createdBy,
        MatchKind matchKind = MatchKind.Regular, Guid? tieBreakEventId = null, string? name = null)
    {
        if (matchKind == MatchKind.TieBreak && tieBreakEventId is null)
        {
            throw new ArgumentException("A TieBreak match must reference its TieBreakEvent.", nameof(tieBreakEventId));
        }

        if (matchKind == MatchKind.Regular && tieBreakEventId is not null)
        {
            throw new ArgumentException("A Regular match must not reference a TieBreakEvent.", nameof(tieBreakEventId));
        }

        return new Match
        {
            ProgramId = programId,
            StageId = stageId,
            MatchNumber = matchNumber,
            MatchKind = matchKind,
            TieBreakEventId = tieBreakEventId,
            Name = name,
            State = MatchState.Draft,
            RandomSeed = randomSeed,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid StageId { get; private set; }
    public int MatchNumber { get; private set; }
    public MatchKind MatchKind { get; private set; }
    public Guid? TieBreakEventId { get; private set; }
    public string? Name { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public MatchState State { get; private set; }
    public Guid? CurrentSegmentId { get; private set; }
    public long RandomSeed { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? PausedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? WinnerTeamId { get; private set; }
    public bool IsTied { get; private set; }
    public string? AbandonReason { get; private set; }
    public Guid? OperatorUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsInSetup => State is MatchState.Draft or MatchState.Ready;

    /// <summary>Any setup edit (participants, segments, details) sends a Ready
    /// match back to Draft, so a match is only ever Ready for the setup it was
    /// last checked against.</summary>
    public void TouchSetup(string updatedBy)
    {
        if (!IsInSetup)
        {
            throw new InvalidStateTransitionException(
                $"Match {MatchNumber} can only be changed before it starts; it is {State}.");
        }

        State = MatchState.Draft;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateDetails(string? name, int matchNumber, string updatedBy)
    {
        TouchSetup(updatedBy);
        Name = name;
        MatchNumber = matchNumber;
    }

    public void MarkReady(string updatedBy)
    {
        if (!IsInSetup)
        {
            throw new InvalidStateTransitionException($"Match {MatchNumber} must be Draft or Ready to be marked Ready; it is {State}.");
        }

        State = MatchState.Ready;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Delete(string deletedBy)
    {
        if (!IsInSetup)
        {
            throw new InvalidStateTransitionException($"Match {MatchNumber} has started and cannot be deleted; abandon it instead.");
        }

        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }

    /// <summary>A match needs at least 2 active participants to start.
    /// <paramref name="activeParticipantCount"/> is supplied by the caller.</summary>
    public void Start(int activeParticipantCount)
    {
        if (State is not (MatchState.Draft or MatchState.Ready))
        {
            throw new InvalidStateTransitionException($"Match {MatchNumber} must be Draft or Ready to start; it is {State}.");
        }

        if (activeParticipantCount < 2)
        {
            throw new InsufficientParticipantsException(
                $"Match {MatchNumber} needs at least 2 active participants to start; it has {activeParticipantCount}.");
        }

        State = MatchState.InProgress;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Pause()
    {
        RequireState(MatchState.InProgress, "paused");
        State = MatchState.Paused;
        PausedAtUtc = DateTime.UtcNow;
    }

    public void Resume()
    {
        RequireState(MatchState.Paused, "resumed");
        State = MatchState.InProgress;
        PausedAtUtc = null;
    }

    public void Complete(Guid? winnerTeamId, bool isTied)
    {
        RequireState(MatchState.InProgress, "completed");
        State = MatchState.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        WinnerTeamId = winnerTeamId;
        IsTied = isTied;
    }

    /// <summary>A completed match's result after a later score correction.</summary>
    public void ReviseResult(Guid? winnerTeamId, bool isTied)
    {
        RequireState(MatchState.Completed, "re-scored");
        WinnerTeamId = winnerTeamId;
        IsTied = isTied;
    }

    public void Abandon(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to abandon a match.", nameof(reason));
        }

        if (State is MatchState.Completed or MatchState.Abandoned)
        {
            throw new InvalidStateTransitionException($"Match {MatchNumber} is already {State}.");
        }

        State = MatchState.Abandoned;
        AbandonReason = reason;
    }

    public void SetCurrentSegment(Guid segmentId)
    {
        CurrentSegmentId = segmentId;
    }

    private void RequireState(MatchState required, string action)
    {
        if (State != required)
        {
            throw new InvalidStateTransitionException(
                $"Match {MatchNumber} must be {required} to be {action}; it is {State}.");
        }
    }
}
