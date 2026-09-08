using Quizware.Domain.Common;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Scoring;

/// <summary>The immutable points ledger. Rows are never updated and never
/// deleted — an undo inserts a new row with the opposite points and
/// ReversesScoreEventId set.</summary>
public sealed class ScoreEvent : BaseEntity, ITenantScoped
{
    private ScoreEvent()
    {
    }

    public static ScoreEvent ForAnswer(
        Guid programId, Guid matchId, Guid teamId, Guid matchParticipantId,
        Guid answerRecordId, Guid scoringRuleId, int points, Guid createdByUserId,
        Guid? matchSegmentId = null)
    {
        return new ScoreEvent
        {
            ProgramId = programId,
            MatchId = matchId,
            MatchSegmentId = matchSegmentId,
            TeamId = teamId,
            MatchParticipantId = matchParticipantId,
            AnswerRecordId = answerRecordId,
            ScoringRuleId = scoringRuleId,
            Points = points,
            EventType = ScoreEventType.Answer,
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
    }

    public static ScoreEvent ManualAdjustment(
        Guid programId, Guid matchId, Guid teamId, Guid matchParticipantId,
        int points, string reason, Guid approvedByUserId,
        Guid? matchSegmentId = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required for a manual score adjustment.", nameof(reason));
        }

        return new ScoreEvent
        {
            ProgramId = programId,
            MatchId = matchId,
            MatchSegmentId = matchSegmentId,
            TeamId = teamId,
            MatchParticipantId = matchParticipantId,
            Points = points,
            EventType = ScoreEventType.ManualAdjust,
            Reason = reason,
            ApprovedByUserId = approvedByUserId,
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = approvedByUserId,
        };
    }

    /// <summary>Undo never mutates the original event — it produces a new,
    /// opposite-signed event that references it.</summary>
    public ScoreEvent Reverse(Guid createdByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reverse a score event.", nameof(reason));
        }

        if (IsReversed)
        {
            throw new InvalidOperationException("This score event has already been reversed.");
        }

        IsReversed = true;

        return new ScoreEvent
        {
            ProgramId = ProgramId,
            MatchId = MatchId,
            MatchSegmentId = MatchSegmentId,
            TeamId = TeamId,
            MatchParticipantId = MatchParticipantId,
            AnswerRecordId = AnswerRecordId,
            ScoringRuleId = ScoringRuleId,
            Points = -Points,
            EventType = ScoreEventType.Reversal,
            Reason = reason,
            ReversesScoreEventId = Id,
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid? MatchSegmentId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid MatchParticipantId { get; private set; }
    public Guid? AnswerRecordId { get; private set; }
    public Guid? ScoringRuleId { get; private set; }
    public int Points { get; private set; }
    public ScoreEventType EventType { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ReversesScoreEventId { get; private set; }
    public bool IsReversed { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
}
