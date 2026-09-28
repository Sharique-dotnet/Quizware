namespace Quizware.Application.Gameplay.Dtos;

public sealed record LiveParticipantDto(
    Guid ParticipantId,
    Guid TeamId,
    string TeamName,
    int SeatNumber,
    int TurnOrder,
    string Status,
    string? ScoreImageUrl,
    int? BuzzDeviceId);

public sealed record LiveSegmentDto(Guid SegmentId, string Format, int OrderIndex, int PlannedQuestionCount, string State);

public sealed record StartMatchResultDto(
    Guid MatchId,
    string State,
    long RandomSeed,
    DateTime StartedAtUtc,
    IReadOnlyList<LiveParticipantDto> Participants,
    IReadOnlyList<LiveSegmentDto> Segments,
    int QuestionsReserved,
    bool BuzzerAvailable);

public sealed record CurrentSegmentDto(
    Guid SegmentId,
    string Format,
    string DisplayName,
    int OrderIndex,
    string State,
    int PlannedQuestionCount,
    int ServedQuestionCount,
    string TopicSelectionMode);

public sealed record CurrentQuestionOptionDto(Guid OptionId, string Text, int DisplayOrder);

public sealed record CurrentQuestionDto(
    Guid MatchQuestionId,
    int OrderIndex,
    string State,
    string Format,
    string? QuestionText,
    byte DifficultyLevel,
    string? TopicName,
    string? MediaUrl,
    IReadOnlyList<CurrentQuestionOptionDto>? Options,
    Guid? CorrectOptionId,
    int? TimeLimitSeconds,
    DateTime? TimerStartedAtUtc,
    DateTime ServerNowUtc,
    double? RemainingSeconds);

public sealed record ActiveParticipantDto(Guid ParticipantId, Guid TeamId, string TeamName, int SeatNumber, int TurnOrder);

public sealed record LiveParticipantScoreDto(Guid ParticipantId, string TeamName, int SeatNumber, int TurnOrder, string Status, int Score);

public sealed record MatchProgressDto(int QuestionsServed, int QuestionsTotal, int SegmentsCompleted, int SegmentsTotal);

public sealed record LiveBuzzerStatusDto(bool Available, Guid? SessionId, string State);

public sealed record LiveMatchStateDto(
    Guid MatchId,
    string MatchName,
    string StageName,
    string State,
    bool IsPaused,
    CurrentSegmentDto? CurrentSegment,
    CurrentQuestionDto? CurrentQuestion,
    ActiveParticipantDto? ActiveParticipant,
    IReadOnlyList<LiveParticipantScoreDto> Participants,
    MatchProgressDto Progress,
    LiveBuzzerStatusDto Buzzer,
    bool CanUndo,
    Guid? LastAnswerId);

public sealed record MatchEventItemDto(long SequenceNumber, string EventType, string? Detail, DateTime OccurredAtUtc);

public sealed record AvailableTopicsDto(IReadOnlyList<string> Topics, int? TopicChoiceLimit);
