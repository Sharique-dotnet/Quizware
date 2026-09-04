using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Qualification;

namespace QuizApp.Domain.Tests.Qualification;

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

        act.Should().Throw<QuizApp.Domain.Common.Exceptions.InvalidStateTransitionException>();
    }
}
