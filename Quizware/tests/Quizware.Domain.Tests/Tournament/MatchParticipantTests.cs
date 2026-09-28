using FluentAssertions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Domain.Tests.Tournament;

public class MatchParticipantTests
{
    [Fact]
    public void Create_DefaultsToActiveStatus()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");

        mp.Status.Should().Be(ParticipantStatus.Active);
        mp.ExcludeFromStandings.Should().BeFalse();
    }

    [Fact]
    public void Disqualify_KeepsScoreButExcludesFromStandings()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");

        mp.Disqualify("Used a phone.", removedBy: Guid.NewGuid());

        mp.Status.Should().Be(ParticipantStatus.Disqualified);
        mp.ExcludeFromStandings.Should().BeTrue();
        mp.RemovalReason.Should().Be("Used a phone.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Disqualify_BlankReason_Throws(string reason)
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");

        var act = () => mp.Disqualify(reason, removedBy: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyTurnOrder_UpdatesTurnOrderOnly()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 3, turnOrder: 1, createdBy: "owner");

        mp.ApplyTurnOrder(2);

        mp.TurnOrder.Should().Be(2);
        mp.SeatNumber.Should().Be(3);
    }

    [Fact]
    public void RecordResult_StoresScoreAndRank_AndAllowsNoRank()
    {
        var ranked = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");
        var unranked = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 2, turnOrder: 2, createdBy: "owner");

        ranked.RecordResult(40, 1);
        unranked.RecordResult(25, null);

        ranked.FinalScore.Should().Be(40);
        ranked.FinalRank.Should().Be(1);
        unranked.FinalScore.Should().Be(25);
        unranked.FinalRank.Should().BeNull();
    }

    [Fact]
    public void Disqualify_CanKeepTheTeamInStandings()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");

        mp.Disqualify("Late arrival", Guid.NewGuid(), excludeFromStandings: false);

        mp.Status.Should().Be(ParticipantStatus.Disqualified);
        mp.ExcludeFromStandings.Should().BeFalse();
    }

    [Fact]
    public void Disqualify_Twice_Throws()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");
        mp.Disqualify("First", Guid.NewGuid());

        var act = () => mp.Disqualify("Second", Guid.NewGuid());

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Reinstate_ClearsTheRemoval_AndSetsTheNewTurn()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");
        mp.Disqualify("Appealed", Guid.NewGuid());

        mp.Reinstate(turnOrder: 3);

        mp.Status.Should().Be(ParticipantStatus.Active);
        mp.RemovalReason.Should().BeNull();
        mp.RemovedAtUtc.Should().BeNull();
        mp.ExcludeFromStandings.Should().BeFalse();
        mp.TurnOrder.Should().Be(3);
        mp.SeatNumber.Should().Be(1);
    }

    [Fact]
    public void Reinstate_AnActiveTeam_Throws()
    {
        var mp = MatchParticipant.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seatNumber: 1, turnOrder: 1, createdBy: "owner");

        var act = () => mp.Reinstate(2);

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
