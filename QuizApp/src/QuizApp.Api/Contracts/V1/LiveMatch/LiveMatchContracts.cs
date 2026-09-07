namespace QuizApp.Api.Contracts.V1.LiveMatch;

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

public sealed record StartMatchResponse(
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

public sealed record LiveMatchStateResponse(
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

public sealed record ServeQuestionRequest(Guid SegmentId);

public sealed record SkipQuestionRequest(string Reason);

public sealed record SkipSegmentRequest(string Reason);

public sealed record RecordAnswerRequest(
    Guid MatchQuestionId,
    Guid MatchParticipantId,
    string Outcome,
    Guid? SelectedOptionId,
    IReadOnlyList<Guid>? SelectedOptionIds,
    string? FreeTextAnswer,
    int PassNumber,
    string AnswerSource,
    Guid? BuzzPressId,
    int? ResponseTimeMs);

public sealed record RankedTeamScoreDto(Guid TeamId, int Score, int Rank);

public sealed record NextQuestionPreviewDto(Guid MatchQuestionId, Guid? ActiveParticipantId, string? ActiveTeamName);

public sealed record RecordAnswerResponse(
    Guid AnswerRecordId,
    string Outcome,
    int PointsAwarded,
    Guid ScoringRuleId,
    int TeamScore,
    IReadOnlyList<RankedTeamScoreDto> Scores,
    NextQuestionPreviewDto? NextQuestion,
    bool SegmentComplete,
    bool MatchComplete);

public sealed record ReverseAnswerRequest(string Reason);

public sealed record PassQuestionRequest(Guid MatchQuestionId, Guid FromParticipantId);

public sealed record SelectTopicRequest(Guid ParticipantId, string TopicName);

public sealed record AvailableTopicsResponse(IReadOnlyList<string> Topics, int? TopicChoiceLimit);

public sealed record DisqualifyParticipantRequest(string Reason, Guid ApprovedByUserId, bool ExcludeFromStandings);

public sealed record CurrentSegmentAdjustmentDto(string Policy, int PlannedQuestionCountBefore, int PlannedQuestionCountAfter, string Message);

public sealed record DisqualifyParticipantResponse(
    Guid ParticipantId,
    string TeamName,
    string Status,
    DateTime RemovedAtUtc,
    IReadOnlyList<LiveParticipantScoreDto> RemainingActiveParticipants,
    bool MatchCanContinue,
    bool TurnOrderRecalculated,
    CurrentSegmentAdjustmentDto? CurrentSegmentAdjustment,
    Guid? NextActiveParticipantId,
    bool MatchCompleted,
    Guid? WinnerTeamId);

public sealed record ReinstateParticipantRequest(string Reason);

public sealed record AbandonMatchRequest(string Reason);

public sealed record MatchEventDto(long SequenceNumber, string EventType, string? Detail, DateTime OccurredAtUtc);

public sealed record MatchTimelineResponse(IReadOnlyList<MatchEventDto> Events);

public sealed record MatchSnapshotResponse(Guid SnapshotId, DateTime CreatedAtUtc);
