using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Tournament;

namespace QuizApp.Domain.Tests.Tournament;

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
}
