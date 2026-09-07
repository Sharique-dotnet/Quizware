namespace QuizApp.Api.Contracts.V1.Display;

public sealed record DisplayMatchStateResponse(
    Guid MatchId,
    string State,
    string? CurrentSegmentFormat,
    string? CurrentQuestionText,
    bool AnswerRevealed);

public sealed record DisplayScoreEntry(Guid TeamId, string TeamName, int Score);

public sealed record DisplayScoresResponse(IReadOnlyList<DisplayScoreEntry> Scores);

public sealed record DisplayStandingEntry(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record DisplayStandingsResponse(IReadOnlyList<DisplayStandingEntry> Standings);

public sealed record DisplayBrandingResponse(string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? FontFamily);
