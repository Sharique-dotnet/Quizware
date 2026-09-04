using FluentAssertions;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Tournament;

namespace QuizApp.Domain.Tests.Tournament;

public class TurnOrderCalculatorTests
{
    private sealed record TestParticipant(Guid Id, int SeatNumber, int TurnOrder, ParticipantStatus Status)
        : ITurnOrderParticipant;

    private static TestParticipant Active(int seat, int turnOrder) =>
        new(Guid.NewGuid(), seat, turnOrder, ParticipantStatus.Active);

    [Fact]
    public void GetNextParticipant_ThreeActive_RotatesInTurnOrder()
    {
        var p1 = Active(seat: 1, turnOrder: 1);
        var p2 = Active(seat: 2, turnOrder: 2);
        var p3 = Active(seat: 3, turnOrder: 3);
        var participants = new ITurnOrderParticipant[] { p1, p2, p3 };

        var actualIds = Enumerable.Range(0, 6)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(participants, questionIndex))
            .ToArray();

        actualIds.Should().Equal(p1.Id, p2.Id, p3.Id, p1.Id, p2.Id, p3.Id);
    }

    [Fact]
    public void GetNextParticipant_OneDisqualifiedMidMatch_RemainingTwoContinueWithNoGap()
    {
        var p1 = Active(seat: 1, turnOrder: 1);
        var p2Active = Active(seat: 2, turnOrder: 2);
        var p3 = Active(seat: 3, turnOrder: 3);
        var before = new ITurnOrderParticipant[] { p1, p2Active, p3 };

        // Questions 0, 1, 2 go to p1, p2, p3 before anything happens.
        var beforeIds = Enumerable.Range(0, 3)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(before, questionIndex))
            .ToArray();
        beforeIds.Should().Equal(p1.Id, p2Active.Id, p3.Id);

        // p2 is disqualified after question index 2. Recompacting assigns the
        // survivors TurnOrder 1 (p1) and 2 (p3), with no gap where p2 was.
        var p2Disqualified = p2Active with { Status = ParticipantStatus.Disqualified };
        var afterDisqualification = new ITurnOrderParticipant[] { p1, p2Disqualified, p3 };
        var recompacted = TurnOrderCalculator.Recompact(afterDisqualification);
        recompacted.Should().Equal(
            new TurnOrderAssignment(p1.Id, 1),
            new TurnOrderAssignment(p3.Id, 2));

        // The rotation then continues over just the two survivors: 1, 2, 1, 2
        // — never a gap, never a fake answer standing in for p2.
        var reconstituted = new ITurnOrderParticipant[]
        {
            p1 with { TurnOrder = 1 },
            p3 with { TurnOrder = 2 },
        };
        var afterIds = Enumerable.Range(0, 4)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(reconstituted, questionIndex))
            .ToArray();

        afterIds.Should().Equal(p1.Id, p3.Id, p1.Id, p3.Id);
    }

    [Fact]
    public void GetNextParticipant_TwoActive_Alternates()
    {
        var p1 = Active(seat: 1, turnOrder: 1);
        var p2 = Active(seat: 2, turnOrder: 2);
        var participants = new ITurnOrderParticipant[] { p1, p2 };

        var actualIds = Enumerable.Range(0, 4)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(participants, questionIndex))
            .ToArray();

        actualIds.Should().Equal(p1.Id, p2.Id, p1.Id, p2.Id);
    }

    [Fact]
    public void GetNextParticipant_FivePresent_RotatesWithoutHardcodedCountAssumption()
    {
        var participants = Enumerable.Range(1, 5)
            .Select(seat => Active(seat, turnOrder: seat))
            .Cast<ITurnOrderParticipant>()
            .ToArray();

        var actualIds = Enumerable.Range(0, 10)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(participants, questionIndex))
            .ToArray();

        var expected = Enumerable.Range(0, 10)
            .Select(i => participants[i % 5].Id);

        actualIds.Should().Equal(expected);
    }

    [Fact]
    public void GetNextParticipant_SingleActiveParticipant_ReturnsSameParticipantEveryTime()
    {
        var p1 = Active(seat: 1, turnOrder: 1);
        var participants = new ITurnOrderParticipant[] { p1 };

        var actualIds = Enumerable.Range(0, 3)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(participants, questionIndex))
            .ToArray();

        actualIds.Should().OnlyContain(id => id == p1.Id);

        // Deciding the match should end with one participant left is the
        // match-lifecycle's responsibility (Phase 9), not this calculator's.
    }

    [Fact]
    public void GetNextParticipant_NoActiveParticipants_Throws()
    {
        var participants = new ITurnOrderParticipant[]
        {
            new TestParticipant(Guid.NewGuid(), SeatNumber: 1, TurnOrder: 1, ParticipantStatus.Disqualified),
        };

        var act = () => TurnOrderCalculator.GetNextParticipant(participants, questionIndex: 0);

        act.Should().Throw<NoActiveParticipantsException>();
    }

    [Fact]
    public void GetNextParticipant_DisqualifiedParticipant_NeverReturnedEvenWithLowestTurnOrder()
    {
        var disqualified = new TestParticipant(Guid.NewGuid(), SeatNumber: 1, TurnOrder: 1, ParticipantStatus.Disqualified);
        var p2 = Active(seat: 2, turnOrder: 2);
        var p3 = Active(seat: 3, turnOrder: 3);
        var participants = new ITurnOrderParticipant[] { disqualified, p2, p3 };

        var actualIds = Enumerable.Range(0, 4)
            .Select(questionIndex => TurnOrderCalculator.GetNextParticipant(participants, questionIndex))
            .ToArray();

        actualIds.Should().NotContain(disqualified.Id);
        actualIds.Should().Equal(p2.Id, p3.Id, p2.Id, p3.Id);
    }

    [Fact]
    public void Recompact_PreservesRelativeOrderOfSurvivors()
    {
        var p1 = Active(seat: 1, turnOrder: 1);
        var disqualified = new TestParticipant(Guid.NewGuid(), SeatNumber: 2, TurnOrder: 2, ParticipantStatus.Disqualified);
        var p3 = Active(seat: 3, turnOrder: 3);
        var p4 = Active(seat: 4, turnOrder: 4);
        var participants = new ITurnOrderParticipant[] { p1, disqualified, p3, p4 };

        var result = TurnOrderCalculator.Recompact(participants);

        result.Should().Equal(
            new TurnOrderAssignment(p1.Id, 1),
            new TurnOrderAssignment(p3.Id, 2),
            new TurnOrderAssignment(p4.Id, 3));
    }

    [Fact]
    public void Recompact_IgnoresSeatNumberOrdering()
    {
        // Seats are out of turn-order sequence on purpose: recompaction must
        // follow TurnOrder, never SeatNumber, and must not touch seating at all.
        var p1 = Active(seat: 3, turnOrder: 1);
        var p2 = Active(seat: 1, turnOrder: 2);
        var participants = new ITurnOrderParticipant[] { p1, p2 };

        var result = TurnOrderCalculator.Recompact(participants);

        result.Select(a => a.ParticipantId).Should().Equal(p1.Id, p2.Id);
    }
}
