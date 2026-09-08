namespace QuizApp.Application.Teams.Dtos;

public sealed record TeamMemberDto(Guid Id, string FullName, string? RollNumber, string? ClassName, bool IsCaptain, string? PhotoUrl);

public sealed record TeamDto(
    Guid Id,
    string Code,
    string SchoolName,
    string DisplayName,
    string? ShortName,
    string? ScoreImageUrl,
    string? SelectionImageUrl,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string Status,
    string? StatusReason,
    DateTime? StatusChangedAtUtc,
    IReadOnlyList<TeamMemberDto> Members);

public sealed record TeamSummaryDto(Guid Id, string Code, string SchoolName, string DisplayName, string Status);

public sealed record TeamHistoryEntryDto(Guid MatchId, string MatchName, int Score, DateTime PlayedAtUtc);
