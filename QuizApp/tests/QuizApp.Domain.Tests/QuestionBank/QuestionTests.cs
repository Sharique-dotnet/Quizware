using FluentAssertions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Domain.Tests.QuestionBank;

public class QuestionTests
{
    [Fact]
    public void Create_ProgramScope_WithoutProgramId_Throws()
    {
        var act = () => McqQuestion.Create(
            programId: null, QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_OrganisationScope_WithProgramId_Throws()
    {
        var act = () => McqQuestion.Create(
            programId: Guid.NewGuid(), QuestionOwnerScope.Organisation, "Q?", DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_StartsAsDraft()
    {
        var question = McqQuestion.Create(Guid.NewGuid(), QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");

        question.Status.Should().Be(QuestionStatus.Draft);
        question.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void Approve_FromDraft_BecomesSelectable()
    {
        var question = McqQuestion.Create(Guid.NewGuid(), QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");
        var approver = Guid.NewGuid();

        question.Approve(approver);

        question.Status.Should().Be(QuestionStatus.Approved);
        question.ApprovedBy.Should().Be(approver);
        question.IsSelectable.Should().BeTrue();
    }

    [Fact]
    public void Approve_Twice_Throws()
    {
        var question = McqQuestion.Create(Guid.NewGuid(), QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");
        question.Approve(Guid.NewGuid());

        var act = () => question.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retire_MakesQuestionNotSelectable()
    {
        var question = McqQuestion.Create(Guid.NewGuid(), QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");
        question.Approve(Guid.NewGuid());

        question.Retire();

        question.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void RecordUsage_IncrementsTimesUsed()
    {
        var question = McqQuestion.Create(Guid.NewGuid(), QuestionOwnerScope.Program, "Q?", DifficultyLevel.Medium, "ur", "author");

        question.RecordUsage();
        question.RecordUsage();

        question.TimesUsed.Should().Be(2);
        question.LastUsedAtUtc.Should().NotBeNull();
    }
}
