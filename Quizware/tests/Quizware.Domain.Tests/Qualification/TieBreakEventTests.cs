using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Domain.Tests.Qualification;

public class TieBreakEventTests
{
    private static TieBreakEvent Detect() =>
        TieBreakEvent.Detect(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TieBreakScope.StageQualification,
            contestedRank: 9, contestedSlots: 1, createdBy: "system");

    [Fact]
    public void Resolve_ByCriteria_NamesTheResolutionMethod()
    {
        var tie = Detect();

        tie.Resolve(TieBreakResolutionMethod.Criteria, Guid.NewGuid(), resolvedByCriterion: "FewerIncorrect");

        tie.State.Should().Be(TieBreakEventState.Resolved);
        tie.ResolutionMethod.Should().Be(TieBreakResolutionMethod.Criteria);
        tie.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_Manually_WithoutNotes_Throws()
    {
        var tie = Detect();

        var act = () => tie.Resolve(TieBreakResolutionMethod.Manual, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resolve_Manually_WithNotes_Succeeds()
    {
        var tie = Detect();

        tie.Resolve(TieBreakResolutionMethod.Manual, Guid.NewGuid(), notes: "Coin toss witnessed by both captains.", approvedByUserId: Guid.NewGuid());

        tie.ResolutionMethod.Should().Be(TieBreakResolutionMethod.Manual);
        tie.Notes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Abandon_AfterResolved_Throws()
    {
        var tie = Detect();
        tie.Resolve(TieBreakResolutionMethod.Criteria, Guid.NewGuid(), resolvedByCriterion: "TotalScore");

        var act = () => tie.Abandon();

        act.Should().Throw<Quizware.Domain.Common.Exceptions.InvalidStateTransitionException>();
    }

    [Fact]
    public void StartPlaying_ThenCancel_GoesBackToAwaitingPlay()
    {
        var tie = Detect();
        tie.AwaitPlay();
        var matchId = Guid.NewGuid();

        tie.StartPlaying(matchId);
        tie.TieBreakMatchId.Should().Be(matchId);
        tie.CancelTieBreakMatch();

        tie.State.Should().Be(TieBreakEventState.AwaitingPlay);
        tie.TieBreakMatchId.Should().BeNull();
    }

    [Fact]
    public void StartPlaying_WhileAlreadyPlaying_Throws()
    {
        var tie = Detect();
        tie.StartPlaying(Guid.NewGuid());

        var act = () => tie.StartPlaying(Guid.NewGuid());

        act.Should().Throw<Quizware.Domain.Common.Exceptions.InvalidStateTransitionException>();
    }

    [Fact]
    public void Resolve_Twice_Throws()
    {
        var tie = Detect();
        tie.Resolve(TieBreakResolutionMethod.Criteria, Guid.NewGuid(), resolvedByCriterion: "FewerIncorrect");

        var act = () => tie.Resolve(TieBreakResolutionMethod.PlayedTieBreak, Guid.NewGuid());

        act.Should().Throw<Quizware.Domain.Common.Exceptions.InvalidStateTransitionException>();
        tie.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Abandon_KeepsTheReason()
    {
        var tie = Detect();

        tie.Abandon("No longer on the qualification boundary.");

        tie.State.Should().Be(TieBreakEventState.Abandoned);
        tie.Notes.Should().Be("No longer on the qualification boundary.");
        var act = () => tie.AwaitPlay();
        act.Should().Throw<Quizware.Domain.Common.Exceptions.InvalidStateTransitionException>();
    }
}
