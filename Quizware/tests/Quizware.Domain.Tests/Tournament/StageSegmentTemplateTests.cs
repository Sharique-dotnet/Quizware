using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Domain.Tests.Tournament;

public class StageSegmentTemplateTests
{
    private static StageSegmentTemplate Create() =>
        StageSegmentTemplate.Create(Guid.NewGuid(), Guid.NewGuid(), QuestionFormatCode.Mcq, 0, questionCount: 5, createdBy: "owner");

    [Fact]
    public void ConfigurePlay_SetsPlaySettings()
    {
        var template = Create();

        template.ConfigurePlay("Round One", 30, TopicSelectionMode.TeamPicksTopic, 3, allowPassing: true, maxPassCount: 2, "editor");

        template.DisplayName.Should().Be("Round One");
        template.TimeLimitSeconds.Should().Be(30);
        template.TopicSelectionMode.Should().Be(TopicSelectionMode.TeamPicksTopic);
        template.TopicChoiceLimit.Should().Be(3);
        template.AllowPassing.Should().BeTrue();
        template.MaxPassCount.Should().Be(2);
    }

    [Fact]
    public void ConfigurePlay_WithoutPassing_ClearsMaxPassCount()
    {
        var template = Create();

        template.ConfigurePlay(null, null, TopicSelectionMode.None, null, allowPassing: false, maxPassCount: 2, "editor");

        template.MaxPassCount.Should().BeNull();
    }

    [Fact]
    public void ConfigurePlay_NonPositiveTimeLimit_Throws()
    {
        var template = Create();

        var act = () => template.ConfigurePlay(null, 0, TopicSelectionMode.None, null, false, null, "editor");

        act.Should().Throw<ArgumentException>();
    }
}
