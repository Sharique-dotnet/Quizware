using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;

namespace Quizware.Domain.Tests.Gameplay;

public class AnswerRecordTests
{
    private static AnswerRecord Create() =>
        AnswerRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), AnswerOutcome.Correct, Guid.NewGuid(), createdBy: "operator");

    [Fact]
    public void MarkReversed_SetsFlagsAndReason()
    {
        var answer = Create();
        var reversalId = Guid.NewGuid();

        answer.MarkReversed(reversalId, "Judges overturned the call.");

        answer.IsReversed.Should().BeTrue();
        answer.ReversedByAnswerId.Should().Be(reversalId);
        answer.ReversalReason.Should().Be("Judges overturned the call.");
    }

    [Fact]
    public void MarkReversed_Twice_Throws()
    {
        var answer = Create();
        answer.MarkReversed(Guid.NewGuid(), "First reversal.");

        var act = () => answer.MarkReversed(Guid.NewGuid(), "Second reversal.");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MarkReversed_BlankReason_Throws(string reason)
    {
        var answer = Create();

        var act = () => answer.MarkReversed(Guid.NewGuid(), reason);

        act.Should().Throw<ArgumentException>();
    }
}
