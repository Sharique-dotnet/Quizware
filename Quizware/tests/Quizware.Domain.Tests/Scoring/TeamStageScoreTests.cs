using FluentAssertions;
using Quizware.Domain.Scoring;

namespace Quizware.Domain.Tests.Scoring;

public class TeamStageScoreTests
{
    private static TeamStageScore Create() => TeamStageScore.CreateForTeam(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void ApplyPoints_MovesOnlyTheTotal()
    {
        var score = Create();

        score.ApplyPoints(10);
        score.ApplyPoints(-4);

        score.TotalPoints.Should().Be(6);
        score.MatchesPlayed.Should().Be(0);
    }

    [Fact]
    public void RecordMatchCompleted_CountsTheMatch_AndTheWinOnlyWhenWon()
    {
        var score = Create();

        score.RecordMatchCompleted(won: true);
        score.RecordMatchCompleted(won: false);

        score.MatchesPlayed.Should().Be(2);
        score.Wins.Should().Be(1);
    }

    [Fact]
    public void Rebuild_OverwritesEveryTotal()
    {
        var score = Create();
        score.ApplyPoints(99);

        score.Rebuild(totalPoints: 30, matchesPlayed: 3, wins: 2);

        score.TotalPoints.Should().Be(30);
        score.MatchesPlayed.Should().Be(3);
        score.Wins.Should().Be(2);
    }
}
