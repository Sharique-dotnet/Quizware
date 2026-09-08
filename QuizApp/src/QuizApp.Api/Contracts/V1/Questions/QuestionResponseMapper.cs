using QuizApp.Application.QuestionBank.Dtos;

namespace QuizApp.Api.Contracts.V1.Questions;

internal static class QuestionResponseMapper
{
    public static QuestionResponse ToResponse(QuestionDto dto) => dto switch
    {
        McqQuestionDto mcq => new McqQuestionResponse
        {
            Id = mcq.Id,
            FormatCode = mcq.FormatCode,
            DifficultyLevel = mcq.DifficultyLevel,
            TopicName = mcq.TopicName,
            QuestionText = mcq.QuestionText,
            Status = mcq.Status,
            SupersedesQuestionId = mcq.SupersedesQuestionId,
            Options = ToOptions(mcq.Options),
            CorrectOptionIds = mcq.CorrectOptionIds,
            AllowMultipleCorrect = mcq.AllowMultipleCorrect,
        },
        BuzzerQuestionDto buzzer => new BuzzerQuestionResponse
        {
            Id = buzzer.Id,
            FormatCode = buzzer.FormatCode,
            DifficultyLevel = buzzer.DifficultyLevel,
            TopicName = buzzer.TopicName,
            QuestionText = buzzer.QuestionText,
            Status = buzzer.Status,
            SupersedesQuestionId = buzzer.SupersedesQuestionId,
            Options = ToOptions(buzzer.Options),
            CorrectOptionIds = buzzer.CorrectOptionIds,
            BuzzWindowSeconds = buzzer.BuzzWindowSeconds,
        },
        PassingQuestionDto passing => new PassingQuestionResponse
        {
            Id = passing.Id,
            FormatCode = passing.FormatCode,
            DifficultyLevel = passing.DifficultyLevel,
            TopicName = passing.TopicName,
            QuestionText = passing.QuestionText,
            Status = passing.Status,
            SupersedesQuestionId = passing.SupersedesQuestionId,
            Options = ToOptions(passing.Options),
            CorrectOptionIds = passing.CorrectOptionIds,
            MaxPassCount = passing.MaxPassCount,
        },
        CardQuestionDto card => new CardQuestionResponse
        {
            Id = card.Id,
            FormatCode = card.FormatCode,
            DifficultyLevel = card.DifficultyLevel,
            TopicName = card.TopicName,
            QuestionText = card.QuestionText,
            Status = card.Status,
            SupersedesQuestionId = card.SupersedesQuestionId,
            Options = ToOptions(card.Options),
            CorrectOptionIds = card.CorrectOptionIds,
        },
        ChoiceQuestionDto choice => new ChoiceQuestionResponse
        {
            Id = choice.Id,
            FormatCode = choice.FormatCode,
            DifficultyLevel = choice.DifficultyLevel,
            TopicName = choice.TopicName,
            QuestionText = choice.QuestionText,
            Status = choice.Status,
            SupersedesQuestionId = choice.SupersedesQuestionId,
            Options = ToOptions(choice.Options),
            CorrectOptionIds = choice.CorrectOptionIds,
        },
        RapidFireQuestionDto rapidFire => new RapidFireQuestionResponse
        {
            Id = rapidFire.Id,
            FormatCode = rapidFire.FormatCode,
            DifficultyLevel = rapidFire.DifficultyLevel,
            TopicName = rapidFire.TopicName,
            QuestionText = rapidFire.QuestionText,
            Status = rapidFire.Status,
            SupersedesQuestionId = rapidFire.SupersedesQuestionId,
            AnswerText = rapidFire.AnswerText,
            IsHostRead = rapidFire.IsHostRead,
        },
        TieBreakerQuestionDto tieBreaker => new TieBreakerQuestionResponse
        {
            Id = tieBreaker.Id,
            FormatCode = tieBreaker.FormatCode,
            DifficultyLevel = tieBreaker.DifficultyLevel,
            TopicName = tieBreaker.TopicName,
            QuestionText = tieBreaker.QuestionText,
            Status = tieBreaker.Status,
            SupersedesQuestionId = tieBreaker.SupersedesQuestionId,
            AnswerMode = tieBreaker.AnswerMode,
            Options = ToOptions(tieBreaker.Options),
            AnswerText = tieBreaker.AnswerText,
            NumericAnswer = tieBreaker.NumericAnswer,
        },
        SequenceQuestionDto sequence => new SequenceQuestionResponse
        {
            Id = sequence.Id,
            FormatCode = sequence.FormatCode,
            DifficultyLevel = sequence.DifficultyLevel,
            TopicName = sequence.TopicName,
            QuestionText = sequence.QuestionText,
            Status = sequence.Status,
            SupersedesQuestionId = sequence.SupersedesQuestionId,
            Items = sequence.Items.Select(i => new Formats.SequenceItemDto(i.Text, i.MediaAssetId, i.CorrectPosition, i.DisplayOrder)).ToList(),
            PartialCreditEnabled = sequence.PartialCreditEnabled,
        },
        AudioVisualQuestionDto audioVisual => new AudioVisualQuestionResponse
        {
            Id = audioVisual.Id,
            FormatCode = audioVisual.FormatCode,
            DifficultyLevel = audioVisual.DifficultyLevel,
            TopicName = audioVisual.TopicName,
            QuestionText = audioVisual.QuestionText,
            Status = audioVisual.Status,
            SupersedesQuestionId = audioVisual.SupersedesQuestionId,
            MediaUrl = $"/api/v1/media/{audioVisual.MediaAssetId}",
            MediaKind = audioVisual.MediaKind,
            AnswerText = audioVisual.AnswerText,
            PlaybackStartSeconds = audioVisual.PlaybackStartSeconds,
            PlaybackDurationSeconds = audioVisual.PlaybackDurationSeconds,
            ReplayAllowed = audioVisual.ReplayAllowed,
        },
        VisualRapidFireQuestionDto visualRapidFire => new VisualRapidFireQuestionResponse
        {
            Id = visualRapidFire.Id,
            FormatCode = visualRapidFire.FormatCode,
            DifficultyLevel = visualRapidFire.DifficultyLevel,
            TopicName = visualRapidFire.TopicName,
            QuestionText = visualRapidFire.QuestionText,
            Status = visualRapidFire.Status,
            SupersedesQuestionId = visualRapidFire.SupersedesQuestionId,
            Items = visualRapidFire.Items.Select(i => new Formats.VrfItemDto(i.MediaAssetId, i.AnswerText, [], i.DisplayOrder)).ToList(),
        },
        _ => throw new NotSupportedException($"Unknown question DTO type: {dto.GetType().Name}"),
    };

    private static IReadOnlyList<OptionResponse> ToOptions(IReadOnlyList<QuestionOptionDto> options) =>
        options.Select(o => new OptionResponse(o.Id, o.Text, o.DisplayOrder, o.MediaAssetId)).ToList();
}
