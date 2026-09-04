using FluentAssertions;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Gameplay;

namespace QuizApp.Domain.Tests.Gameplay;

public class MatchQuestionTests
{
    private static MatchQuestion Reserve(Guid segmentId, int orderIndex) =>
        MatchQuestion.Reserve(Guid.NewGuid(), Guid.NewGuid(), segmentId, Guid.NewGuid(), orderIndex, createdBy: "owner");

    [Fact]
    public void Activate_WhenNoOtherQuestionActive_Succeeds()
    {
        var segmentId = Guid.NewGuid();
        var question = Reserve(segmentId, 1);
        var other = Reserve(segmentId, 2);

        question.Activate(new[] { other }, optionOrderJson: "[1,2,3]", timeLimitSeconds: 30);

        question.State.Should().Be(MatchQuestionState.Active);
        question.ServedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Activate_WhenAnotherQuestionAlreadyActive_Throws()
    {
        var segmentId = Guid.NewGuid();
        var activeAlready = Reserve(segmentId, 1);
        activeAlready.Activate(Array.Empty<MatchQuestion>(), "[]", 30);
        var next = Reserve(segmentId, 2);

        var act = () => next.Activate(new[] { activeAlready }, "[]", 30);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void MarkAnswered_WhenNotActive_Throws()
    {
        var question = Reserve(Guid.NewGuid(), 1);

        var act = () => question.MarkAnswered();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void MarkAnswered_WhenActive_ClosesTheQuestion()
    {
        var question = Reserve(Guid.NewGuid(), 1);
        question.Activate(Array.Empty<MatchQuestion>(), "[]", 30);

        question.MarkAnswered();

        question.State.Should().Be(MatchQuestionState.Answered);
        question.ClosedAtUtc.Should().NotBeNull();
    }
}
