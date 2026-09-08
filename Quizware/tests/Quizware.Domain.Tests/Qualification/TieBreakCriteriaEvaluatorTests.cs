using FluentAssertions;
using Quizware.Domain.Qualification;

namespace Quizware.Domain.Tests.Qualification;

public class TieBreakCriteriaEvaluatorTests
{
    [Fact]
    public void Evaluate_FirstCriterionSeparatesToOneWinner_ReportsThatCriterion()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var candidates = new[]
        {
            new TieBreakCandidate(teamA, new Dictionary<string, decimal> { ["TotalScore"] = 50 }),
            new TieBreakCandidate(teamB, new Dictionary<string, decimal> { ["TotalScore"] = 45 }),
        };

        var result = TieBreakCriteriaEvaluator.Evaluate(candidates, new[] { "TotalScore", "FewerIncorrect" });

        result.IsResolved.Should().BeTrue();
        result.DecidingCriterion.Should().Be("TotalScore");
        result.TeamIds.Should().Equal(teamA);
    }

    [Fact]
    public void Evaluate_FirstCriterionTiesEveryone_FallsThroughToSecond()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        // Criterion values are pre-normalised so higher always means better —
        // "FewerIncorrect" is stored as -IncorrectCount, so team B (1 wrong)
        // beats team A (3 wrong): -1 > -3.
        var candidates = new[]
        {
            new TieBreakCandidate(teamA, new Dictionary<string, decimal> { ["TotalScore"] = 50, ["FewerIncorrect"] = -3 }),
            new TieBreakCandidate(teamB, new Dictionary<string, decimal> { ["TotalScore"] = 50, ["FewerIncorrect"] = -1 }),
        };

        var result = TieBreakCriteriaEvaluator.Evaluate(candidates, new[] { "TotalScore", "FewerIncorrect" });

        result.IsResolved.Should().BeTrue();
        result.DecidingCriterion.Should().Be("FewerIncorrect");
        result.TeamIds.Should().Equal(teamB);
    }

    [Fact]
    public void Evaluate_NoCriterionSeparates_StillTied()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var candidates = new[]
        {
            new TieBreakCandidate(teamA, new Dictionary<string, decimal> { ["TotalScore"] = 50 }),
            new TieBreakCandidate(teamB, new Dictionary<string, decimal> { ["TotalScore"] = 50 }),
        };

        var result = TieBreakCriteriaEvaluator.Evaluate(candidates, new[] { "TotalScore" });

        result.IsResolved.Should().BeFalse();
        result.TeamIds.Should().BeEquivalentTo(new[] { teamA, teamB });
    }
}
