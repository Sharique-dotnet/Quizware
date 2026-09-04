using FluentAssertions;
using QuizApp.Domain.Common.Exceptions;
using QuizApp.Domain.Enums;
using QuizApp.Domain.Programs;

namespace QuizApp.Domain.Tests.Programs;

public class ProgramTests
{
    private static Program CreateDraftProgram() =>
        Program.Create(code: "9AMM-2026", name: "9AMM Inter-School Quiz 2026", createdBy: "owner");

    [Fact]
    public void Create_SetsDraftStateAndDefaults()
    {
        var program = CreateDraftProgram();

        program.State.Should().Be(ProgramState.Draft);
        program.AllowNegativeTotals.Should().BeTrue();
        program.QuestionPoolScope.Should().Be(QuestionPoolScope.ProgramOnly);
        program.DefaultLanguage.Should().Be("ur");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankCode_Throws(string code)
    {
        var act = () => Program.Create(code, "Name", "owner");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Configure_FromDraft_TransitionsToConfigured()
    {
        var program = CreateDraftProgram();

        program.Configure();

        program.State.Should().Be(ProgramState.Configured);
    }

    [Fact]
    public void Configure_WhenNotDraft_Throws()
    {
        var program = CreateDraftProgram();
        program.Configure();

        var act = () => program.Configure();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void GoLive_WithAtLeastOneStage_TransitionsToLive()
    {
        var program = CreateDraftProgram();
        program.Configure();

        program.GoLive(stageCount: 1);

        program.State.Should().Be(ProgramState.Live);
    }

    [Fact]
    public void GoLive_WithNoStages_Throws()
    {
        var program = CreateDraftProgram();
        program.Configure();

        var act = () => program.GoLive(stageCount: 0);

        act.Should().Throw<InvalidStateTransitionException>();
        program.State.Should().Be(ProgramState.Configured);
    }

    [Fact]
    public void GoLive_WhenStillDraft_Throws()
    {
        var program = CreateDraftProgram();

        var act = () => program.GoLive(stageCount: 3);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Complete_FromLive_TransitionsToCompletedAndStampsCompletedAtUtc()
    {
        var program = CreateDraftProgram();
        program.Configure();
        program.GoLive(stageCount: 1);

        program.Complete();

        program.State.Should().Be(ProgramState.Completed);
        program.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Archive_FromCompleted_TransitionsToArchived()
    {
        var program = CreateDraftProgram();
        program.Configure();
        program.GoLive(stageCount: 1);
        program.Complete();

        program.Archive();

        program.State.Should().Be(ProgramState.Archived);
    }

    [Fact]
    public void Archive_WhenNotCompleted_Throws()
    {
        var program = CreateDraftProgram();

        var act = () => program.Archive();

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
