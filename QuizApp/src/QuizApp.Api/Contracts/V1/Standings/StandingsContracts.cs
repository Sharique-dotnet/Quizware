namespace QuizApp.Api.Contracts.V1.Standings;

public sealed record StandingEntry(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record OverallStandingsResponse(IReadOnlyList<StandingEntry> Standings);

public sealed record StageStandingsResponse(Guid StageId, IReadOnlyList<StandingEntry> Standings, IReadOnlyList<string> TieBreakNotes);

public sealed record TeamStandingRecord(Guid MatchId, string MatchName, int Score, int Rank);

public sealed record TeamStandingResponse(Guid TeamId, IReadOnlyList<TeamStandingRecord> Matches, int OverallScore, int OverallRank);
