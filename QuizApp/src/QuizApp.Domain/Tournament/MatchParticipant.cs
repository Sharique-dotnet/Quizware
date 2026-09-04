using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Tournament;

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

    /// <summary>Score is kept but excluded from standings — never zeroed —
    /// so match history is not lost.</summary>
    public void Disqualify(string reason, Guid removedBy, Guid? removedAtSegmentId = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to disqualify a participant.", nameof(reason));
        }

        Status = ParticipantStatus.Disqualified;
        RemovalReason = reason;
        RemovedAtUtc = DateTime.UtcNow;
        RemovedBy = removedBy;
        RemovedAtSegmentId = removedAtSegmentId;
        ExcludeFromStandings = true;
    }

    /// <summary>Applies a TurnOrderCalculator.Recompact result to this participant.</summary>
    public void ApplyTurnOrder(int newTurnOrder)
    {
        TurnOrder = newTurnOrder;
    }
}
