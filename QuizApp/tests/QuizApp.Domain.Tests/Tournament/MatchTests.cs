using FluentAssertions;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Tournament;

namespace QuizApp.Domain.Tests.Tournament;

public class MatchTests
{
    private static Match Create() =>
        Match.Create(Guid.NewGuid(), Guid.NewGuid(), matchNumber: 1, randomSeed: 42, createdBy: "owner");

    [Fact]
    public void Start_WithFewerThanTwoParticipants_Throws()
    {
        var match = Create();

        var act = () => match.Start(activeParticipantCount: 1);

        act.Should().Throw<InsufficientParticipantsException>();
        match.State.Should().Be(MatchState.Draft);
    }

    [Fact]
    public void Start_WithTwoOrMoreParticipants_Succeeds()
    {
        var match = Create();

        match.Start(activeParticipantCount: 2);

        match.State.Should().Be(MatchState.InProgress);
        match.StartedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void PauseAndResume_RoundTrips()
    {
        var match = Create();
        match.Start(activeParticipantCount: 3);

        match.Pause();
        match.State.Should().Be(MatchState.Paused);

        match.Resume();
        match.State.Should().Be(MatchState.InProgress);
    }

    [Fact]
    public void Create_TieBreakKind_WithoutTieBreakEventId_Throws()
    {
        var act = () => Match.Create(
            Guid.NewGuid(), Guid.NewGuid(), matchNumber: 1, randomSeed: 1, createdBy: "owner",
            matchKind: MatchKind.TieBreak, tieBreakEventId: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Abandon_BlankReason_Throws()
    {
        var match = Create();
        match.Start(activeParticipantCount: 2);

        var act = () => match.Abandon(" ");

        act.Should().Throw<ArgumentException>();
    }
}
