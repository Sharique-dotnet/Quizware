using Quizware.Domain.Enums;

namespace Quizware.Application.Selection;

/// <summary>What to draw. <see cref="MatchId"/>/<see cref="TeamId"/> are only
/// needed for a real (reserving) draw — a preview has neither a match nor a
/// target team yet, so NeverInMatch/NeverForTeam simply do not exclude
/// anything during a preview.</summary>
public sealed record SelectionRequest(
    Guid ProgramId,
    Guid StageId,
    Guid? SegmentTemplateId,
    QuestionFormatCode FormatCode,
    int QuestionCount,
    long RandomSeed,
    Guid? MatchId = null,
    Guid? TeamId = null);

public sealed record SelectedQuestion(
    Guid QuestionId,
    DifficultyLevel DifficultyLevel,
    Guid? TopicId,
    string? OptionOrderJson);

public sealed record SelectionResult(
    IReadOnlyList<SelectedQuestion> Questions,
    int PoolSize,
    int EligibleAfterFilters,
    int EligibleAfterRepeatPolicy,
    IReadOnlyDictionary<string, int> DifficultyMixRequested,
    IReadOnlyDictionary<string, int> DifficultyMixAchieved,
    bool CanSatisfy,
    IReadOnlyList<string> Warnings);
