namespace Quizware.Api.Contracts.V1.Scores;

public sealed record TeamScoreDto(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record MatchScoresResponse(IReadOnlyList<TeamScoreDto> Scores);

public sealed record ScoreEventDto(Guid Id, Guid TeamId, int Points, string Reason, bool IsReversed, DateTime OccurredAtUtc);

public sealed record ScoreEventsResponse(IReadOnlyList<ScoreEventDto> Events);

public sealed record AdjustScoreRequest(Guid TeamId, int PointsDelta, string Reason);

public sealed record RecalculateScoresResponse(IReadOnlyList<TeamScoreDto> Scores);
