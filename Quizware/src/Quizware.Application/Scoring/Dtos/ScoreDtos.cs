namespace Quizware.Application.Scoring.Dtos;

public sealed record TeamScoreItemDto(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record ScoreEventItemDto(Guid Id, Guid TeamId, int Points, string Reason, bool IsReversed, DateTime OccurredAtUtc);
