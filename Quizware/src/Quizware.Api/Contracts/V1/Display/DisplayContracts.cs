namespace Quizware.Api.Contracts.V1.Display;

/// <summary>The first five fields are the original (Phase 5) contract; the
/// rest were added in P12-07 so a screen can render the question, the team
/// on turn and the scores from one call. The correct option stays null until
/// the question is revealed.</summary>
public sealed record DisplayMatchStateResponse(
    Guid MatchId,
    string State,
    string? CurrentSegmentFormat,
    string? CurrentQuestionText,
    bool AnswerRevealed,
    string MatchName,
    string StageName,
    bool IsPaused,
    LiveMatch.CurrentQuestionDto? CurrentQuestion,
    LiveMatch.ActiveParticipantDto? ActiveParticipant,
    IReadOnlyList<LiveMatch.LiveParticipantScoreDto> Participants,
    LiveMatch.MatchProgressDto Progress);

public sealed record DisplayScoreEntry(Guid TeamId, string TeamName, int Score);

public sealed record DisplayScoresResponse(IReadOnlyList<DisplayScoreEntry> Scores);

public sealed record DisplayStandingEntry(Guid TeamId, string TeamName, int Score, int Rank);

public sealed record DisplayStandingsResponse(IReadOnlyList<DisplayStandingEntry> Standings);

public sealed record DisplayBrandingResponse(string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? FontFamily);
