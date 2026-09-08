using System.Text.Json.Serialization;
using QuizApp.Api.Contracts.V1.Questions.Formats;

namespace QuizApp.Api.Contracts.V1.Questions;

/// <summary>
/// Polymorphic question detail (05-API-Design.md §5.8): one response type per
/// format, discriminated by <see cref="FormatCode"/>. System.Text.Json's
/// polymorphic serialization emits/reads the discriminator, and Swashbuckle
/// maps it to an OpenAPI `oneOf` + `discriminator/mapping`, which is what
/// generates a discriminated union in the TypeScript client.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "formatCode")]
[JsonDerivedType(typeof(McqQuestionResponse), "Mcq")]
[JsonDerivedType(typeof(BuzzerQuestionResponse), "Buzzer")]
[JsonDerivedType(typeof(PassingQuestionResponse), "Passing")]
[JsonDerivedType(typeof(CardQuestionResponse), "Card")]
[JsonDerivedType(typeof(ChoiceQuestionResponse), "Choice")]
[JsonDerivedType(typeof(RapidFireQuestionResponse), "RapidFire")]
[JsonDerivedType(typeof(TieBreakerQuestionResponse), "TieBreaker")]
[JsonDerivedType(typeof(SequenceQuestionResponse), "Sequence")]
[JsonDerivedType(typeof(AudioVisualQuestionResponse), "AudioVisual")]
[JsonDerivedType(typeof(VisualRapidFireQuestionResponse), "VisualRapidFire")]
public abstract record QuestionResponse
{
    public required Guid Id { get; init; }
    public required string FormatCode { get; init; }
    public required byte DifficultyLevel { get; init; }
    public string? TopicName { get; init; }
    public string? QuestionText { get; init; }
    public required string Status { get; init; }
    public Guid? SupersedesQuestionId { get; init; }
}

public sealed record OptionResponse(Guid Id, string Text, int DisplayOrder, Guid? MediaAssetId);

public abstract record OptionBasedQuestionResponse : QuestionResponse
{
    public required IReadOnlyList<OptionResponse> Options { get; init; }
    public IReadOnlyList<Guid>? CorrectOptionIds { get; init; }
}

public sealed record McqQuestionResponse : OptionBasedQuestionResponse
{
    public bool AllowMultipleCorrect { get; init; }
}

public sealed record BuzzerQuestionResponse : OptionBasedQuestionResponse
{
    public int BuzzWindowSeconds { get; init; }
}

public sealed record PassingQuestionResponse : OptionBasedQuestionResponse
{
    public int MaxPassCount { get; init; }
}

public sealed record CardQuestionResponse : OptionBasedQuestionResponse;

public sealed record ChoiceQuestionResponse : OptionBasedQuestionResponse
{
    public int? TopicChoiceLimit { get; init; }
}

/// <summary>Contract fix: RapidFireQuestion has no options table at all
/// (it's either a stored answer or read off paper) — the stub incorrectly
/// inherited the option-based shape.</summary>
public sealed record RapidFireQuestionResponse : QuestionResponse
{
    public string? AnswerText { get; init; }
    public bool IsHostRead { get; init; }
}

/// <summary>Contract fix: the stub had no way to represent which of the
/// three answer modes a tie-breaker question uses.</summary>
public sealed record TieBreakerQuestionResponse : QuestionResponse
{
    public required string AnswerMode { get; init; }
    public IReadOnlyList<OptionResponse> Options { get; init; } = [];
    public string? AnswerText { get; init; }
    public decimal? NumericAnswer { get; init; }
}

public sealed record SequenceQuestionResponse : QuestionResponse
{
    public required IReadOnlyList<SequenceItemDto> Items { get; init; }
    public bool PartialCreditEnabled { get; init; }
}

public sealed record AudioVisualQuestionResponse : QuestionResponse
{
    public required string MediaUrl { get; init; }
    public required string MediaKind { get; init; }
    public required string AnswerText { get; init; }
    public int? PlaybackStartSeconds { get; init; }
    public int? PlaybackDurationSeconds { get; init; }
    public bool ReplayAllowed { get; init; }
}

public sealed record VisualRapidFireQuestionResponse : QuestionResponse
{
    public required IReadOnlyList<VrfItemDto> Items { get; init; }
}

public sealed record QuestionSummaryResponse(Guid Id, string FormatCode, byte DifficultyLevel, string? TopicName, string Status, string? QuestionText);

public sealed record QuestionCoverageRequirement(string Format, IReadOnlyList<int> DifficultyRange, int RequiredTotal, int Available, string Status, int? Shortfall);

public sealed record QuestionCoverageByStage(string StageName, int Matches, IReadOnlyList<QuestionCoverageRequirement> Requirements);

public sealed record QuestionCoverageResponse(
    bool ReadyToRun,
    IReadOnlyList<string> FormatsInUse,
    IReadOnlyList<string> FormatsNotUsed,
    IReadOnlyList<QuestionCoverageByStage> ByStage,
    IReadOnlyList<string> Blockers);

public sealed record QuestionUsageEntry(Guid MatchId, string MatchName, DateTime PlayedAtUtc, bool AnsweredCorrectly);

public sealed record QuestionUsageResponse(IReadOnlyList<QuestionUsageEntry> UsedIn);

public sealed record DuplicateQuestionPair(Guid QuestionId, Guid DuplicateOfQuestionId, double SimilarityScore);

public sealed record QuestionDuplicatesResponse(IReadOnlyList<DuplicateQuestionPair> Pairs);

public sealed record ApproveQuestionRequest(string? Notes);

public sealed record RetireQuestionRequest(string Reason);

public sealed record QuestionImportValidateResponse(Guid BatchId, int RowCount, int ValidRowCount, IReadOnlyList<string> Errors);

public sealed record QuestionImportCommitResponse(int QuestionsCreated);
