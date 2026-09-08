namespace QuizApp.Application.QuestionBank.Dtos;

public sealed record QuestionCoverageRequirementDto(string Format, int RequiredTotal, int Available, string Status, int? Shortfall);

public sealed record QuestionCoverageByStageDto(string StageName, IReadOnlyList<QuestionCoverageRequirementDto> Requirements);

public sealed record QuestionCoverageDto(
    bool ReadyToRun,
    IReadOnlyList<string> FormatsInUse,
    IReadOnlyList<string> FormatsNotUsed,
    IReadOnlyList<QuestionCoverageByStageDto> ByStage,
    IReadOnlyList<string> Blockers);

public sealed record QuestionUsageEntryDto(Guid MatchId, string MatchName, DateTime PlayedAtUtc);

public sealed record DuplicateQuestionPairDto(Guid QuestionId, Guid DuplicateOfQuestionId, double SimilarityScore);
