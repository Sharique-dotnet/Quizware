using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;

namespace Quizware.Domain.Tests.Scoring;

public class TeamMatchScoreTests
{
    [Fact]
    public void Rebuild_OverwritesTheTotalAndEveryCount()
    {
        var score = TeamMatchScore.CreateForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        score.ApplyAnswer(AnswerOutcome.Correct, 50);

        score.Rebuild(totalPoints: 7, correctCount: 2, incorrectCount: 1, noAnswerCount: 3, passedCount: 4);

        score.TotalPoints.Should().Be(7);
        score.CorrectCount.Should().Be(2);
        score.IncorrectCount.Should().Be(1);
        score.NoAnswerCount.Should().Be(3);
        score.PassedCount.Should().Be(4);
    }

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

    [Fact]
    public void RevertAnswer_UndoesApplyAnswerExactly()
    {
        var score = TeamMatchScore.CreateForParticipant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        score.ApplyAnswer(AnswerOutcome.Correct, 10);
        score.ApplyAnswer(AnswerOutcome.Incorrect, -5);

        score.RevertAnswer(AnswerOutcome.Incorrect, -5);

        score.TotalPoints.Should().Be(10);
        score.CorrectCount.Should().Be(1);
        score.IncorrectCount.Should().Be(0);
    }
}
