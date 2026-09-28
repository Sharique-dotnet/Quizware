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

    [Fact]
    public void Delete_PendingSegment_SoftDeletes()
    {
        var segment = Create(Guid.NewGuid(), 0);

        segment.Delete("owner");

        segment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Delete_OpenSegment_Throws()
    {
        var segment = Create(Guid.NewGuid(), 0);
        segment.Open([]);

        var act = () => segment.Delete("owner");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Renumber_MovesALockedSegment()
    {
        var segment = Create(Guid.NewGuid(), 3);
        segment.Lock();

        segment.Renumber(1);

        segment.OrderIndex.Should().Be(1);
    }

    [Fact]
    public void AdjustPlannedQuestionCount_NeverBelowServed()
    {
        var segment = Create(Guid.NewGuid(), 0);
        segment.Open([]);
        segment.RecordQuestionServed();
        segment.RecordQuestionServed();

        segment.AdjustPlannedQuestionCount(2);
        var act = () => segment.AdjustPlannedQuestionCount(1);

        segment.PlannedQuestionCount.Should().Be(2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdjustPlannedQuestionCount_OnACompletedSegment_Throws()
    {
        var segment = Create(Guid.NewGuid(), 0);
        segment.Open([]);
        segment.Complete();

        var act = () => segment.AdjustPlannedQuestionCount(3);

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
