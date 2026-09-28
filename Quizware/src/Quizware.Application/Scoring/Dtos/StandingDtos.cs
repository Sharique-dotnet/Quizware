namespace Quizware.Application.Scoring.Dtos;

public sealed record StandingEntryDto(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record StageStandingsDto(Guid StageId, IReadOnlyList<StandingEntryDto> Standings, IReadOnlyList<string> TieBreakNotes);

public sealed record TeamMatchRecordDto(Guid MatchId, string MatchName, int Score, int Rank);

public sealed record TeamStandingDto(Guid TeamId, IReadOnlyList<TeamMatchRecordDto> Matches, int OverallScore, int OverallRank);
