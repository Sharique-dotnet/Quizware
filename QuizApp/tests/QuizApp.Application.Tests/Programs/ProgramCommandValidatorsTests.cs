using FluentValidation.TestHelper;
using QuizApp.Application.Programs.Commands;
using QuizApp.Domain.Enums;

namespace QuizApp.Application.Tests.Programs;

public class ProgramCommandValidatorsTests
{
    [Fact]
    public void CreateProgram_ValidRequest_Passes()
    {
        var command = new CreateProgramCommand("2026", "Inter-School Quiz 2026", "The annual tournament");

        new CreateProgramCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateProgram_BlankCode_Fails()
    {
        var command = new CreateProgramCommand(string.Empty, "Inter-School Quiz 2026", null);

        new CreateProgramCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void CreateProgram_BlankName_Fails()
    {
        var command = new CreateProgramCommand("2026", string.Empty, null);

        new CreateProgramCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateProgram_MissingId_Fails()
    {
        var command = new UpdateProgramCommand(Guid.Empty, "Name", null, null, null, null, null, null, null);

        new UpdateProgramCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void UpdateProgram_ValidRequest_Passes()
    {
        var command = new UpdateProgramCommand(Guid.NewGuid(), "Name", "Description", null, null, null, null, null, 20);

        new UpdateProgramCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateProgram_NonPositiveMaxTeams_Fails()
    {
        var command = new UpdateProgramCommand(Guid.NewGuid(), "Name", null, null, null, null, null, null, 0);

        new UpdateProgramCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.MaxTeams);
    }

    [Fact]
    public void CloneProgram_MissingNewCode_Fails()
    {
        var command = new CloneProgramCommand(Guid.NewGuid(), string.Empty, "New Program");

        new CloneProgramCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.NewProgramCode);
    }

    [Fact]
    public void CloneProgram_ValidRequest_Passes()
    {
        var command = new CloneProgramCommand(Guid.NewGuid(), "2027", "Inter-School Quiz 2027");

        new CloneProgramCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateProgramFormats_DisablingWithoutReason_Fails()
    {
        var command = new UpdateProgramFormatsCommand(
            Guid.NewGuid(),
            [new ProgramFormatEntryCommand(QuestionFormatCode.Mcq, false, null)]);

        new UpdateProgramFormatsCommandValidator().TestValidate(command)
            .ShouldHaveValidationErrorFor("Formats[0].DisabledReason");
    }

    [Fact]
    public void UpdateProgramFormats_DisablingWithReason_Passes()
    {
        var command = new UpdateProgramFormatsCommand(
            Guid.NewGuid(),
            [new ProgramFormatEntryCommand(QuestionFormatCode.Mcq, false, "No longer used this season")]);

        new UpdateProgramFormatsCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateProgramFormats_EmptyList_Fails()
    {
        var command = new UpdateProgramFormatsCommand(Guid.NewGuid(), []);

        new UpdateProgramFormatsCommandValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.Formats);
    }

    [Fact]
    public void UpdateProgramSettings_BlankCategory_Fails()
    {
        var command = new UpdateProgramSettingsCommand(
            Guid.NewGuid(),
            [new ProgramSettingEntryCommand(string.Empty, "MaxTeams", "20")]);

        new UpdateProgramSettingsCommandValidator().TestValidate(command)
            .ShouldHaveValidationErrorFor("Settings[0].Category");
    }

    [Fact]
    public void UpdateProgramSettings_ValidEntry_Passes()
    {
        var command = new UpdateProgramSettingsCommand(
            Guid.NewGuid(),
            [new ProgramSettingEntryCommand("Teams", "MaxTeams", "20")]);

        new UpdateProgramSettingsCommandValidator().TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
