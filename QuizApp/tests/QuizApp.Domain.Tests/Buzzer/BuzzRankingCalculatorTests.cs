using FluentAssertions;
using QuizApp.Domain.Buzzer;

namespace QuizApp.Domain.Tests.Buzzer;

public class BuzzRankingCalculatorTests
{
    [Fact]
    public void Rank_LowestNonZeroTimeWins()
    {
        var fastest = Guid.NewGuid();
        var slowest = Guid.NewGuid();
        var readings = new[]
        {
            new BuzzPressReading(slowest, 800),
            new BuzzPressReading(fastest, 250),
        };

        var ranking = BuzzRankingCalculator.Rank(readings);

        ranking[0].ParticipantId.Should().Be(fastest);
        ranking[0].Rank.Should().Be(1);
        ranking[1].ParticipantId.Should().Be(slowest);
    }

    [Fact]
    public void Rank_ZeroMeansNoPressAndSortsLast()
    {
        var didNotPress = Guid.NewGuid();
        var pressed = Guid.NewGuid();
        var readings = new[]
        {
            new BuzzPressReading(didNotPress, 0),
            new BuzzPressReading(pressed, 999),
        };

        var ranking = BuzzRankingCalculator.Rank(readings);

        ranking.Last().ParticipantId.Should().Be(didNotPress);
    }
}
