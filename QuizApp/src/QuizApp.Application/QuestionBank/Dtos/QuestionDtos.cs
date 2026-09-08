namespace QuizApp.Application.QuestionBank.Dtos;

public abstract record QuestionDto
{
    public required Guid Id { get; init; }
    public required string FormatCode { get; init; }
    public required byte DifficultyLevel { get; init; }
    public string? TopicName { get; init; }
    public string? QuestionText { get; init; }
    public required string Status { get; init; }
    public Guid? SupersedesQuestionId { get; init; }
}

public sealed record QuestionOptionDto(Guid Id, string Text, int DisplayOrder, Guid? MediaAssetId);

public abstract record OptionBasedQuestionDto : QuestionDto
{
    public required IReadOnlyList<QuestionOptionDto> Options { get; init; }
    public required IReadOnlyList<Guid> CorrectOptionIds { get; init; }
}

public sealed record McqQuestionDto : OptionBasedQuestionDto
{
    public bool AllowMultipleCorrect { get; init; }
}

public sealed record BuzzerQuestionDto : OptionBasedQuestionDto
{
    public int BuzzWindowSeconds { get; init; }
}

public sealed record PassingQuestionDto : OptionBasedQuestionDto
{
    public int MaxPassCount { get; init; }
}

public sealed record CardQuestionDto : OptionBasedQuestionDto;

public sealed record ChoiceQuestionDto : OptionBasedQuestionDto
{
    public required string TopicLabel { get; init; }
    public int? TopicDisplayOrder { get; init; }
}

public sealed record RapidFireQuestionDto : QuestionDto
{
    public string? AnswerText { get; init; }
    public bool IsHostRead { get; init; }
}

public sealed record TieBreakerQuestionDto : QuestionDto
{
    public required string AnswerMode { get; init; }
    public IReadOnlyList<QuestionOptionDto> Options { get; init; } = [];
    public IReadOnlyList<Guid> CorrectOptionIds { get; init; } = [];
    public string? AnswerText { get; init; }
    public decimal? NumericAnswer { get; init; }
}

public sealed record SequenceItemDto(Guid Id, string? Text, Guid? MediaAssetId, int CorrectPosition, int DisplayOrder);

public sealed record SequenceQuestionDto : QuestionDto
{
    public required IReadOnlyList<SequenceItemDto> Items { get; init; }
    public bool PartialCreditEnabled { get; init; }
}

public sealed record AudioVisualQuestionDto : QuestionDto
{
    public required Guid MediaAssetId { get; init; }
    public required string MediaKind { get; init; }
    public required string AnswerText { get; init; }
    public int? PlaybackStartSeconds { get; init; }
    public int? PlaybackDurationSeconds { get; init; }
    public bool ReplayAllowed { get; init; }
}

public sealed record VrfItemDto(Guid Id, Guid MediaAssetId, string AnswerText, int DisplayOrder);

public sealed record VisualRapidFireQuestionDto : QuestionDto
{
    public required IReadOnlyList<VrfItemDto> Items { get; init; }
}

public sealed record QuestionSummaryDto(Guid Id, string FormatCode, byte DifficultyLevel, string? TopicName, string Status, string? QuestionText);
