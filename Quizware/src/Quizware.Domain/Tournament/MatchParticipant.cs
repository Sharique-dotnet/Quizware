using Quizware.Domain.Common;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Tournament;

/// <summary>The fix for the disqualification problem: SeatNumber is the
/// physical position and never changes; TurnOrder drives the answering
/// rotation and is recalculated when a team leaves (see
/// <see cref="TurnOrderCalculator"/>).</summary>
public sealed class MatchParticipant : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable, ITurnOrderParticipant
{
    private MatchParticipant()
    {
        CreatedBy = string.Empty;
    }

    public static MatchParticipant Create(
        Guid programId, Guid matchId, Guid teamId, int seatNumber, int turnOrder, string createdBy)
    {
        return new MatchParticipant
        {
            ProgramId = programId,
            MatchId = matchId,
            TeamId = teamId,
            SeatNumber = seatNumber,
            TurnOrder = turnOrder,
            Status = ParticipantStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid TeamId { get; private set; }
    public int SeatNumber { get; private set; }
    public int TurnOrder { get; private set; }
    public ParticipantStatus Status { get; private set; }
    public string? RemovalReason { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public Guid? RemovedBy { get; private set; }
    public Guid? RemovedAtSegmentId { get; private set; }
    public Guid? SubstitutedByTeamId { get; private set; }
    public bool ExcludeFromStandings { get; private set; }
    public int FinalScore { get; private set; }
    public int? FinalRank { get; private set; }
    public int? BuzzDeviceId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Score is kept — never zeroed — so match history is not lost;
    /// by default it is also excluded from standings.</summary>
    public void Disqualify(string reason, Guid removedBy, Guid? removedAtSegmentId = null, bool excludeFromStandings = true)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to disqualify a participant.", nameof(reason));
        }

        if (Status != ParticipantStatus.Active)
        {
            throw new InvalidStateTransitionException($"Only an active participant can be disqualified; this one is {Status}.");
        }

        Status = ParticipantStatus.Disqualified;
        RemovalReason = reason;
        RemovedAtUtc = DateTime.UtcNow;
        RemovedBy = removedBy;
        RemovedAtSegmentId = removedAtSegmentId;
        ExcludeFromStandings = excludeFromStandings;
    }

    /// <summary>Undoes a disqualification. The team rejoins the rotation at
    /// <paramref name="turnOrder"/> (the caller puts it last, then recompacts).</summary>
    public void Reinstate(int turnOrder)
    {
        if (Status != ParticipantStatus.Disqualified)
        {
            throw new InvalidStateTransitionException($"Only a disqualified participant can be reinstated; this one is {Status}.");
        }

        Status = ParticipantStatus.Active;
        RemovalReason = null;
        RemovedAtUtc = null;
        RemovedBy = null;
        RemovedAtSegmentId = null;
        ExcludeFromStandings = false;
        TurnOrder = turnOrder;
    }

    public void SetSeatAndTurn(int seatNumber, int turnOrder, string updatedBy)
    {
        SeatNumber = seatNumber;
        TurnOrder = turnOrder;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Setup-time removal only — a participant removed during play
    /// is disqualified or withdrawn instead, so its history survives.</summary>
    public void Delete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }

    /// <summary>Written once when the match completes. A participant excluded
    /// from standings keeps its score but gets no rank.</summary>
    public void RecordResult(int finalScore, int? finalRank)
    {
        FinalScore = finalScore;
        FinalRank = finalRank;
    }

    /// <summary>Applies a TurnOrderCalculator.Recompact result to this participant.</summary>
    public void ApplyTurnOrder(int newTurnOrder)
    {
        TurnOrder = newTurnOrder;
    }
}
