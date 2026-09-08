using FluentAssertions;
using Quizware.Domain.Gameplay;

namespace Quizware.Domain.Tests.Gameplay;

public class MatchEventTests
{
    [Fact]
    public void NextSequenceNumber_NoExistingEvents_ReturnsOne()
    {
        MatchEvent.NextSequenceNumber(Array.Empty<MatchEvent>()).Should().Be(1);
    }

    [Fact]
    public void NextSequenceNumber_WithExistingEvents_IsStrictlyIncreasing()
    {
        var matchId = Guid.NewGuid();
        var first = MatchEvent.Create(Guid.NewGuid(), matchId, 1, "MatchStarted", "{}");
        var second = MatchEvent.Create(Guid.NewGuid(), matchId, MatchEvent.NextSequenceNumber(new[] { first }), "SegmentOpened", "{}");

        second.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public void Create_SequenceNumberBelowOne_Throws()
    {
        var act = () => MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), 0, "MatchStarted", "{}");

        act.Should().Throw<ArgumentException>();
    }
}
