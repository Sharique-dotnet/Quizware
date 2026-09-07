using FluentAssertions;
using FluentValidation.TestHelper;
using QuizApp.Api.Contracts.V1.Questions.Formats;

namespace QuizApp.Api.IntegrationTests.Validation;

public class QuestionFormatValidatorsTests
{
    [Fact]
    public void Mcq_ExactlyOneCorrectOption_Passes()
    {
        var request = new CreateMcqQuestionRequest
        {
            FormatCode = "Mcq",
            QuestionText = "What is 2 + 2?",
            DifficultyLevelId = 2,
            Options =
            [
                new OptionDto("3", false, 1, null),
                new OptionDto("4", true, 2, null),
            ],
        };

        new CreateMcqQuestionValidator().TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Mcq_NoCorrectOption_Fails()
    {
        var request = new CreateMcqQuestionRequest
        {
            FormatCode = "Mcq",
            QuestionText = "What is 2 + 2?",
            DifficultyLevelId = 2,
            Options =
            [
                new OptionDto("3", false, 1, null),
                new OptionDto("5", false, 2, null),
            ],
        };

        new CreateMcqQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.Options);
    }

    [Fact]
    public void Mcq_SingleOption_FailsMinimumCount()
    {
        var request = new CreateMcqQuestionRequest
        {
            FormatCode = "Mcq",
            QuestionText = "Too few options",
            DifficultyLevelId = 2,
            Options = [new OptionDto("Only one", true, 1, null)],
        };

        new CreateMcqQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.Options);
    }

    [Fact]
    public void Mcq_AllowMultipleCorrect_SkipsExactlyOneRule()
    {
        var request = new CreateMcqQuestionRequest
        {
            FormatCode = "Mcq",
            QuestionText = "Pick all primes",
            DifficultyLevelId = 2,
            AllowMultipleCorrect = true,
            Options =
            [
                new OptionDto("2", true, 1, null),
                new OptionDto("3", true, 2, null),
                new OptionDto("4", false, 3, null),
            ],
        };

        new CreateMcqQuestionValidator().TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Sequence_ContiguousPositions_Passes()
    {
        var request = new CreateSequenceQuestionRequest
        {
            FormatCode = "Sequence",
            DifficultyLevelId = 2,
            Items =
            [
                new SequenceItemDto("First", null, 1, 1),
                new SequenceItemDto("Second", null, 2, 2),
                new SequenceItemDto("Third", null, 3, 3),
            ],
        };

        new CreateSequenceQuestionValidator().TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Sequence_GapInPositions_Fails()
    {
        var request = new CreateSequenceQuestionRequest
        {
            FormatCode = "Sequence",
            DifficultyLevelId = 2,
            Items =
            [
                new SequenceItemDto("First", null, 1, 1),
                new SequenceItemDto("Third", null, 3, 2),
            ],
        };

        new CreateSequenceQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Sequence_PartialCreditWithoutPoints_Fails()
    {
        var request = new CreateSequenceQuestionRequest
        {
            FormatCode = "Sequence",
            DifficultyLevelId = 2,
            PartialCreditEnabled = true,
            Items =
            [
                new SequenceItemDto("First", null, 1, 1),
                new SequenceItemDto("Second", null, 2, 2),
            ],
        };

        new CreateSequenceQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.PointsPerCorrectPosition);
    }

    [Fact]
    public void AudioVisual_MissingMediaAsset_Fails()
    {
        var request = new CreateAudioVisualQuestionRequest
        {
            FormatCode = "AudioVisual",
            DifficultyLevelId = 2,
            MediaAssetId = Guid.Empty,
            MediaKind = QuizApp.Domain.Enums.MediaKind.Audio,
            AnswerText = "Ghalib",
        };

        new CreateAudioVisualQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.MediaAssetId);
    }

    [Fact]
    public void AudioVisual_ValidRequest_Passes()
    {
        var request = new CreateAudioVisualQuestionRequest
        {
            FormatCode = "AudioVisual",
            DifficultyLevelId = 3,
            MediaAssetId = Guid.NewGuid(),
            MediaKind = QuizApp.Domain.Enums.MediaKind.Audio,
            AnswerText = "Ghalib",
        };

        new CreateAudioVisualQuestionValidator().TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Base_DifficultyOutOfRange_Fails()
    {
        var request = new CreateMcqQuestionRequest
        {
            FormatCode = "Mcq",
            QuestionText = "x",
            DifficultyLevelId = 9,
            Options = [new OptionDto("a", true, 1, null), new OptionDto("b", false, 2, null)],
        };

        new CreateMcqQuestionValidator().TestValidate(request).ShouldHaveValidationErrorFor(x => x.DifficultyLevelId);
    }
}
