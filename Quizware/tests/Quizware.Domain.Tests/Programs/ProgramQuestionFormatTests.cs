using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Programs;

namespace Quizware.Domain.Tests.Programs;

public class ProgramQuestionFormatTests
{
    [Fact]
    public void Create_DefaultsToEnabled()
    {
        var pqf = ProgramQuestionFormat.Create(Guid.NewGuid(), QuestionFormatCode.Mcq, createdBy: "owner");

        pqf.IsEnabled.Should().BeTrue();
        pqf.DisabledReason.Should().BeNull();
    }

    [Fact]
    public void Disable_SetsReasonAndClearsOnEnable()
    {
        var pqf = ProgramQuestionFormat.Create(Guid.NewGuid(), QuestionFormatCode.Buzzer, createdBy: "owner");

        pqf.Disable("No buzzer hardware this year.", updatedBy: "admin");

        pqf.IsEnabled.Should().BeFalse();
        pqf.DisabledReason.Should().Be("No buzzer hardware this year.");

        pqf.Enable(updatedBy: "admin");

        pqf.IsEnabled.Should().BeTrue();
        pqf.DisabledReason.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Disable_BlankReason_Throws(string reason)
    {
        var pqf = ProgramQuestionFormat.Create(Guid.NewGuid(), QuestionFormatCode.Choice, createdBy: "owner");

        var act = () => pqf.Disable(reason, updatedBy: "admin");

        act.Should().Throw<ArgumentException>();
    }
}
