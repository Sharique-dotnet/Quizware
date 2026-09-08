using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Domain.Tests.QuestionBank;

public class FormatSpecificQuestionTests
{
    [Fact]
    public void AudioVisualQuestion_EmptyMediaAssetId_Throws()
    {
        var act = () => AudioVisualQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, "What clip is this?",
            mediaAssetId: Guid.Empty, MediaKind.Video, answerText: "Answer",
            DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AudioVisualQuestion_BlankAnswerText_Throws()
    {
        var act = () => AudioVisualQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, "What clip is this?",
            mediaAssetId: Guid.NewGuid(), MediaKind.Video, answerText: "  ",
            DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AudioVisualQuestion_ValidInputs_Constructs()
    {
        var question = AudioVisualQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, "What clip is this?",
            mediaAssetId: Guid.NewGuid(), MediaKind.Video, answerText: "Ghalib",
            DifficultyLevel.Hard, "ur", "author");

        question.AnswerText.Should().Be("Ghalib");
        question.FormatCode.Should().Be(QuestionFormatCode.AudioVisual);
    }

    [Fact]
    public void RapidFireQuestion_HostRead_HasNoAnswerText()
    {
        var question = RapidFireQuestion.CreateHostRead(
            Guid.NewGuid(), QuestionOwnerScope.Program, DifficultyLevel.Easy, "ur", "author");

        question.IsHostRead.Should().BeTrue();
        question.AnswerText.Should().BeNull();
    }

    [Fact]
    public void RapidFireQuestion_Stored_RequiresAnswerText()
    {
        var act = () => RapidFireQuestion.CreateStored(
            Guid.NewGuid(), QuestionOwnerScope.Program, "Capital of France?", answerText: "",
            DifficultyLevel.Easy, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SequenceQuestion_LengthBelowTwo_Throws()
    {
        var act = () => SequenceQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, "Order these", sequenceLength: 1,
            DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VisualRapidFireQuestion_ZeroImages_Throws()
    {
        var act = () => VisualRapidFireQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, imageCount: 0,
            DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChoiceQuestion_BlankTopicLabel_Throws()
    {
        var act = () => ChoiceQuestion.Create(
            Guid.NewGuid(), QuestionOwnerScope.Program, "Pick a topic", topicLabel: " ",
            DifficultyLevel.Medium, "ur", "author");

        act.Should().Throw<ArgumentException>();
    }
}
