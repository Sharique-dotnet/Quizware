using QuizApp.Domain.Common;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Qualification;

/// <summary>The permanent, auditable record that a tie occurred and how it
/// was settled — a disputed wildcard place can be defended after the event.</summary>
public sealed class TieBreakEvent : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private TieBreakEvent()
    {
        CreatedBy = string.Empty;
    }

    public static TieBreakEvent Detect(
        Guid programId, Guid stageId, Guid tieBreakRuleId, TieBreakScope scope,
        int contestedRank, int contestedSlots, string createdBy, Guid? sourceMatchId = null)
    {
        return new TieBreakEvent
        {
            ProgramId = programId,
            StageId = stageId,
            TieBreakRuleId = tieBreakRuleId,
            Scope = scope,
            SourceMatchId = sourceMatchId,
            ContestedRank = contestedRank,
            ContestedSlots = contestedSlots,
            State = TieBreakEventState.Detected,
            DetectedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid StageId { get; private set; }
    public Guid TieBreakRuleId { get; private set; }
    public TieBreakScope Scope { get; private set; }
    public Guid? SourceMatchId { get; private set; }
    public int ContestedRank { get; private set; }
    public int ContestedSlots { get; private set; }
    public TieBreakEventState State { get; private set; }
    public TieBreakResolutionMethod? ResolutionMethod { get; private set; }
    public string? ResolvedByCriterion { get; private set; }
    public Guid? TieBreakMatchId { get; private set; }
    public int RoundsPlayed { get; private set; }
    public DateTime DetectedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void StartPlaying(Guid tieBreakMatchId)
    {
        State = TieBreakEventState.InProgress;
        TieBreakMatchId = tieBreakMatchId;
    }

    public void RecordExtraRound()
    {
        RoundsPlayed++;
    }

    /// <summary>A resolved tie must name its resolution method — never left implicit.</summary>
    public void Resolve(
        TieBreakResolutionMethod resolutionMethod, Guid resolvedByUserId,
        string? resolvedByCriterion = null, string? notes = null, Guid? approvedByUserId = null)
    {
        if (resolutionMethod == TieBreakResolutionMethod.Manual && string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException("Notes are required when resolving a tie manually.", nameof(notes));
        }

        ResolutionMethod = resolutionMethod;
        ResolvedByCriterion = resolvedByCriterion;
        ResolvedByUserId = resolvedByUserId;
        ApprovedByUserId = approvedByUserId;
        Notes = notes;
        ResolvedAtUtc = DateTime.UtcNow;
        State = TieBreakEventState.Resolved;
    }

    public void Abandon()
    {
        if (State == TieBreakEventState.Resolved)
        {
            throw new InvalidStateTransitionException("A resolved tie-break cannot be abandoned.");
        }

        State = TieBreakEventState.Abandoned;
    }
}
