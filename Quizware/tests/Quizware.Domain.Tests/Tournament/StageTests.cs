using FluentAssertions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Domain.Tests.Tournament;

public class StageTests
{
    private static Stage Create() =>
        Stage.Create(Guid.NewGuid(), "League", orderIndex: 1, StageType.League, createdBy: "owner");

    [Fact]
    public void MarkReady_WithNoSegments_Throws()
    {
        var stage = Create();

        var act = () => stage.MarkReady(segmentCount: 0);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void MarkReady_WithAtLeastOneSegment_Succeeds()
    {
        var stage = Create();

        stage.MarkReady(segmentCount: 1);

        stage.State.Should().Be(StageState.Ready);
    }

    [Fact]
    public void FullLifecycle_DraftToCompleted()
    {
        var stage = Create();

        stage.MarkReady(segmentCount: 4);
        stage.Start();
        stage.Complete();

        stage.State.Should().Be(StageState.Completed);
        stage.StartedAtUtc.Should().NotBeNull();
        stage.CompletedAtUtc.Should().NotBeNull();
    }
}
