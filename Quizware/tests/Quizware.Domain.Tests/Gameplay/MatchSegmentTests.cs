using FluentAssertions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;

namespace Quizware.Domain.Tests.Gameplay;

public class MatchSegmentTests
{
    private static MatchSegment Create(Guid matchId, int orderIndex) =>
        MatchSegment.Create(Guid.NewGuid(), matchId, Guid.NewGuid(), QuestionFormatCode.Mcq, orderIndex, plannedQuestionCount: 5, createdBy: "owner");

    [Fact]
    public void Open_WhenNoOtherSegmentOpen_Succeeds()
    {
        var matchId = Guid.NewGuid();
        var segment = Create(matchId, 1);
        var other = Create(matchId, 2);

        segment.Open(new[] { other });

        segment.State.Should().Be(MatchSegmentState.Open);
    }

    [Fact]
    public void Open_WhenAnotherSegmentAlreadyOpen_Throws()
    {
        var matchId = Guid.NewGuid();
        var alreadyOpen = Create(matchId, 1);
        alreadyOpen.Open(Array.Empty<MatchSegment>());
        var next = Create(matchId, 2);

        var act = () => next.Open(new[] { alreadyOpen });

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Reorder_LockedSegment_Throws()
    {
        var segment = Create(Guid.NewGuid(), 1);
        segment.Lock();

        var act = () => segment.Reorder(2);

        act.Should().Throw<SegmentNotReorderableException>();
    }

    [Fact]
    public void Reorder_AlreadyOpenSegment_Throws()
    {
        var segment = Create(Guid.NewGuid(), 1);
        segment.Open(Array.Empty<MatchSegment>());

        var act = () => segment.Reorder(2);

        act.Should().Throw<SegmentNotReorderableException>();
    }
}
