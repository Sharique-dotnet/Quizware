using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Scoring;

namespace QuizApp.Domain.Tests.Scoring;

public class ScoreEventTests
{
    [Fact]
    public void Reverse_ProducesOppositePointsAndReferencesOriginal()
    {
        var original = ScoreEvent.ForAnswer(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), points: 20, createdByUserId: Guid.NewGuid());

        var reversal = original.Reverse(Guid.NewGuid(), "Judges overturned the call.");

        reversal.Points.Should().Be(-20);
        reversal.EventType.Should().Be(ScoreEventType.Reversal);
        reversal.ReversesScoreEventId.Should().Be(original.Id);
        original.IsReversed.Should().BeTrue();
        original.Points.Should().Be(20, "the original row is never mutated in place");
    }

    [Fact]
    public void Reverse_Twice_Throws()
    {
        var original = ScoreEvent.ForAnswer(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), points: 10, createdByUserId: Guid.NewGuid());
        original.Reverse(Guid.NewGuid(), "First reversal.");

        var act = () => original.Reverse(Guid.NewGuid(), "Second reversal.");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ManualAdjustment_BlankReason_Throws(string reason)
    {
        var act = () => ScoreEvent.ManualAdjustment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            points: -5, reason, approvedByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }
}
