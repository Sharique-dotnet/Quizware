using Quizware.Domain.Common;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Gameplay;

/// <summary>The served-question log. Questions are drawn at match start and
/// reserved here, so a crash does not change what comes next and the same
/// question cannot be served twice.</summary>
public sealed class MatchQuestion : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private MatchQuestion()
    {
        CreatedBy = string.Empty;
    }

    public static MatchQuestion Reserve(
        Guid programId, Guid matchId, Guid matchSegmentId, Guid questionId, int orderIndex, string createdBy)
    {
        return new MatchQuestion
        {
            ProgramId = programId,
            MatchId = matchId,
            MatchSegmentId = matchSegmentId,
            QuestionId = questionId,
            OrderIndex = orderIndex,
            State = MatchQuestionState.Reserved,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid MatchSegmentId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int OrderIndex { get; private set; }
    public Guid? TargetTeamId { get; private set; }
    public Guid? TargetParticipantId { get; private set; }
    public Guid? SelectedTopicId { get; private set; }
    public MatchQuestionState State { get; private set; }
    public string? OptionOrderJson { get; private set; }
    public DateTime? ServedAtUtc { get; private set; }
    public DateTime? RevealedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public int? TimeLimitSeconds { get; private set; }
    public DateTime? TimerStartedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void AssignTarget(Guid? targetTeamId, Guid? targetParticipantId)
    {
        TargetTeamId = targetTeamId;
        TargetParticipantId = targetParticipantId;
    }

    /// <summary>Only one question may be Active per segment — the caller
    /// passes every other question in the same segment so this can be checked.</summary>
    public void Activate(IReadOnlyList<MatchQuestion> otherQuestionsInSegment, string optionOrderJson, int? timeLimitSeconds)
    {
        if (State != MatchQuestionState.Reserved)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} must be Reserved to activate; it is {State}.");
        }

        if (otherQuestionsInSegment.Any(q => q.Id != Id && q.MatchSegmentId == MatchSegmentId && q.State == MatchQuestionState.Active))
        {
            throw new InvalidStateTransitionException(
                "Segment already has an active question; another cannot also be activated.");
        }

        State = MatchQuestionState.Active;
        OptionOrderJson = optionOrderJson;
        TimeLimitSeconds = timeLimitSeconds;
        ServedAtUtc = DateTime.UtcNow;
        TimerStartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reveals the answer to everyone watching. A question that has
    /// not been served yet (or was released unplayed) has nothing to reveal.</summary>
    public void Reveal()
    {
        if (State is MatchQuestionState.Reserved or MatchQuestionState.Released)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} has not been served; it is {State}.");
        }

        RevealedAtUtc ??= DateTime.UtcNow;
    }

    /// <summary>Records the topic a team picked for this (still reserved) question.</summary>
    public void SelectTopic(Guid topicId)
    {
        if (State != MatchQuestionState.Reserved)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} must be Reserved to take a topic; it is {State}.");
        }

        SelectedTopicId = topicId;
    }

    /// <summary>Re-sequences a reserved question within its segment — used
    /// when a team's topic pick brings a later question forward.</summary>
    public void Renumber(int newOrderIndex)
    {
        if (State != MatchQuestionState.Reserved)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} must be Reserved to move; it is {State}.");
        }

        OrderIndex = newOrderIndex;
    }

    public void MarkAnswered()
    {
        RequireActive();
        State = MatchQuestionState.Answered;
        ClosedAtUtc = DateTime.UtcNow;
    }

    public void Skip()
    {
        RequireActive();
        State = MatchQuestionState.Skipped;
        ClosedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Puts an answered question back on screen after its answer was
    /// reversed, so the correct outcome can be recorded.</summary>
    public void Reopen()
    {
        if (State != MatchQuestionState.Answered)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} must be Answered to reopen; it is {State}.");
        }

        State = MatchQuestionState.Active;
        ClosedAtUtc = null;
    }

    public void Release()
    {
        State = MatchQuestionState.Released;
        ClosedAtUtc = DateTime.UtcNow;
    }

    private void RequireActive()
    {
        if (State != MatchQuestionState.Active)
        {
            throw new InvalidStateTransitionException($"Question at position {OrderIndex} must be Active; it is {State}.");
        }
    }
}
