using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Scoring;

namespace QuizApp.Domain.Tests.Scoring;

public class TeamMatchScoreTests
{
    [Fact]
    public void ApplyAnswer_Correct_AddsPointsAndIncrementsCorrectCount()
    {
        var score = TeamMatchScore.CreateForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        score.ApplyAnswer(AnswerOutcome.Correct, 10);
        score.ApplyAnswer(AnswerOutcome.Incorrect, -5);

        score.TotalPoints.Should().Be(5);
        score.CorrectCount.Should().Be(1);
        score.IncorrectCount.Should().Be(1);
    }

    [Fact]
    public void ApplyAdjustment_ChangesTotalPointsOnly()
    {
        var score = TeamMatchScore.CreateForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        score.ApplyAnswer(AnswerOutcome.Correct, 10);

        score.ApplyAdjustment(-3);

        score.TotalPoints.Should().Be(7);
        score.CorrectCount.Should().Be(1);
    }
}
