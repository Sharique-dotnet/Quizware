using QuizApp.Domain.Enums;

namespace QuizApp.Api.Contracts.V1.Questions.Formats;

/// <summary>Shared fields for every per-format create request (05-API-Design.md §5.8).</summary>
public abstract record CreateQuestionRequestBase
{
    public required string FormatCode { get; init; }
    public string? QuestionText { get; init; }
    public string? Explanation { get; init; }
    public required byte DifficultyLevelId { get; init; }
    public Guid? TopicId { get; init; }
    public string Language { get; init; } = "ur";
    public int? TimeLimitSeconds { get; init; }
    public string? Source { get; init; }
    public List<Guid> TagIds { get; init; } = [];
}

public sealed record OptionDto(string Text, bool IsCorrect, int DisplayOrder, Guid? MediaAssetId);

public sealed record SequenceItemDto(string? Text, Guid? MediaAssetId, int CorrectPosition, int DisplayOrder);

public sealed record VrfItemDto(Guid MediaAssetId, string AnswerText, List<string> AcceptableAnswers, int DisplayOrder);

public sealed record CreateMcqQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
    public bool AllowMultipleCorrect { get; init; }
    public bool ShuffleOptions { get; init; } = true;
    public bool NegativeMarkingEnabled { get; init; }
}

public sealed record CreateBuzzerQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
    public int BuzzWindowSeconds { get; init; } = 30;
    public bool LockoutOnWrongAnswer { get; init; } = true;
    public bool AllowStealAfterWrong { get; init; } = true;
    public int? StealWindowSeconds { get; init; }
}

public sealed record CreatePassingQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
    public int MaxPassCount { get; init; } = 2;
    public PassDirection PassDirection { get; init; } = PassDirection.Clockwise;
    public bool RevealAnswerIfAllPass { get; init; } = true;
}

public sealed record CreateCardQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
}

public sealed record CreateChoiceQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
    public int? TopicChoiceLimit { get; init; }
    public bool IsExclusiveTopic { get; init; }
}

public sealed record CreateRapidFireQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
}

public sealed record CreateTieBreakerQuestionRequest : CreateQuestionRequestBase
{
    public required List<OptionDto> Options { get; init; }
    public decimal? NumericAnswer { get; init; }
}

public sealed record CreateSequenceQuestionRequest : CreateQuestionRequestBase
{
    public required List<SequenceItemDto> Items { get; init; }
    public bool PartialCreditEnabled { get; init; }
    public int? PointsPerCorrectPosition { get; init; }
    public SequenceItemKind ItemKind { get; init; } = SequenceItemKind.Text;
}

public sealed record CreateAudioVisualQuestionRequest : CreateQuestionRequestBase
{
    public required Guid MediaAssetId { get; init; }
    public required MediaKind MediaKind { get; init; }
    public required string AnswerText { get; init; }
    public List<string> AcceptableAnswers { get; init; } = [];
    public int? PlaybackStartSeconds { get; init; }
    public int? PlaybackDurationSeconds { get; init; }
    public bool AutoPlay { get; init; }
    public bool ReplayAllowed { get; init; } = true;
    public Guid? RevealMediaAssetId { get; init; }
}

public sealed record CreateVisualRapidFireQuestionRequest : CreateQuestionRequestBase
{
    public required List<VrfItemDto> Items { get; init; }
    public int? RevealSecondsPerImage { get; init; }
    public int? GridColumns { get; init; }
    public bool ScorePerImage { get; init; } = true;
}
