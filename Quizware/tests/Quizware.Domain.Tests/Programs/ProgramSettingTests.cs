using FluentAssertions;
using Quizware.Domain.Programs;

namespace Quizware.Domain.Tests.Programs;

public class ProgramSettingTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var programId = Guid.NewGuid();

        var setting = ProgramSetting.Create(
            programId,
            category: "Gameplay",
            key: "MaxPassCount",
            value: "2",
            valueType: "int",
            createdBy: "owner",
            description: "Maximum times a question may be passed.");

        setting.ProgramId.Should().Be(programId);
        setting.Category.Should().Be("Gameplay");
        setting.Key.Should().Be("MaxPassCount");
        setting.Value.Should().Be("2");
        setting.ValueType.Should().Be("int");
        setting.Description.Should().Be("Maximum times a question may be passed.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankCategory_Throws(string category)
    {
        var act = () => ProgramSetting.Create(Guid.NewGuid(), category, "Key", "Value", "string", "owner");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankKey_Throws(string key)
    {
        var act = () => ProgramSetting.Create(Guid.NewGuid(), "Category", key, "Value", "string", "owner");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateValue_ReplacesValueAndStampsAudit()
    {
        var setting = ProgramSetting.Create(Guid.NewGuid(), "Scoring", "MaxTeams", "18", "int", "owner");

        setting.UpdateValue("24", "admin");

        setting.Value.Should().Be("24");
        setting.UpdatedBy.Should().Be("admin");
        setting.UpdatedAtUtc.Should().NotBeNull();
    }
}
