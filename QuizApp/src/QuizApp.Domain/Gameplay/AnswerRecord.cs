using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Gameplay;

/// <summary>Replaces all nine legacy *_Answers tables with one, carrying a
/// real foreign key to the question.</summary>
public sealed class AnswerRecord : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private AnswerRecord()
    {
        CreatedBy = string.Empty;
    }

    public static AnswerRecord Create(
        Guid programId, Guid matchId, Guid matchSegmentId, Guid matchQuestionId,
        Guid teamId, Guid matchParticipantId, AnswerOutcome outcome, Guid recordedByUserId,
        string createdBy, int passNumber = 0, AnswerSource answerSource = AnswerSource.Operator,
        string? idempotencyKey = null)
    {
        return new AnswerRecord
        {
            ProgramId = programId,
            MatchId = matchId,
            MatchSegmentId = matchSegmentId,
            MatchQuestionId = matchQuestionId,
            TeamId = teamId,
            MatchParticipantId = matchParticipantId,
            Outcome = outcome,
            PassNumber = passNumber,
            AnswerSource = answerSource,
            AnsweredAtUtc = DateTime.UtcNow,
            RecordedByUserId = recordedByUserId,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid MatchSegmentId { get; private set; }
    public Guid MatchQuestionId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid MatchParticipantId { get; private set; }
    public AnswerOutcome Outcome { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public string? SelectedOptionIdsJson { get; private set; }
    public string? FreeTextAnswer { get; private set; }
    public bool? IsCorrect { get; private set; }
    public int PassNumber { get; private set; }
    public AnswerSource AnswerSource { get; private set; }
    public Guid? BuzzPressId { get; private set; }
    public int? ResponseTimeMs { get; private set; }
    public DateTime AnsweredAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public bool IsReversed { get; private set; }
    public Guid? ReversedByAnswerId { get; private set; }
    public string? ReversalReason { get; private set; }
    public string? IdempotencyKey { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Marks this record as reversed by a compensating record —
    /// never deleted, never mutated in place beyond this marker.</summary>
    public void MarkReversed(Guid reversedByAnswerId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reverse an answer.", nameof(reason));
        }

        if (IsReversed)
        {
            throw new InvalidOperationException("This answer has already been reversed.");
        }

        IsReversed = true;
        ReversedByAnswerId = reversedByAnswerId;
        ReversalReason = reason;
    }
}
