using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Tournament;

public interface ITurnOrderParticipant
{
    Guid Id { get; }
    int SeatNumber { get; }
    int TurnOrder { get; }
    ParticipantStatus Status { get; }
}

public sealed record TurnOrderAssignment(Guid ParticipantId, int TurnOrder);

/// <summary>
/// Replaces the legacy `QuestionNumber % 3` rule: turn order rotates over
/// active participants only, so disqualification never produces a fake answer.
/// </summary>
public static class TurnOrderCalculator
{
    public static Guid GetNextParticipant(IReadOnlyList<ITurnOrderParticipant> participants, int questionIndex)
    {
        var active = ActiveOrderedByTurn(participants);

        if (active.Count == 0)
        {
            throw new NoActiveParticipantsException();
        }

        return active[questionIndex % active.Count].Id;
    }

    /// <summary>
    /// Renumbers TurnOrder to a contiguous 1..N sequence over active
    /// participants only, preserving their relative order. SeatNumber is
    /// untouched — teams do not move on stage.
    /// </summary>
    public static IReadOnlyList<TurnOrderAssignment> Recompact(IReadOnlyList<ITurnOrderParticipant> participants)
    {
        return ActiveOrderedByTurn(participants)
            .Select((participant, index) => new TurnOrderAssignment(participant.Id, index + 1))
            .ToList();
    }

    private static List<ITurnOrderParticipant> ActiveOrderedByTurn(IReadOnlyList<ITurnOrderParticipant> participants)
    {
        return participants
            .Where(p => p.Status == ParticipantStatus.Active)
            .OrderBy(p => p.TurnOrder)
            .ToList();
    }
}
