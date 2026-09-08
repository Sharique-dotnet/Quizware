namespace QuizApp.Api.Contracts.V1.Teams;

public sealed record TeamSummaryResponse(Guid Id, string Code, string SchoolName, string DisplayName, string Status);

public sealed record TeamMemberDto(Guid Id, string FullName, string? RollNumber, string? ClassName, bool IsCaptain, string? PhotoUrl);

public sealed record TeamDetailResponse(
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

public sealed record CreateTeamRequest(string Code, string SchoolName, string DisplayName, IReadOnlyList<string> MemberNames);

public sealed record UpdateTeamRequest(
    string SchoolName,
    string DisplayName,
    string? ShortName,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail);

public sealed record ChangeTeamStatusRequest(string Status, string Reason);

/// <summary>No file-upload pipeline exists yet (IMediaService is P6-15) —
/// images are client-supplied URLs, same scope decision as Phase 6a's
/// Program.LogoUrl.</summary>
public sealed record SetTeamImagesRequest(string? ScoreImageUrl, string? SelectionImageUrl);

public sealed record TeamImportRowResult(int RowNumber, bool IsValid, IReadOnlyList<string> Errors);

public sealed record TeamImportValidateResponse(
    Guid BatchId, int RowCount, int ValidRowCount, int InvalidRowCount, IReadOnlyList<TeamImportRowResult> Rows);

public sealed record TeamImportCommitResponse(int TeamsCreated);

public sealed record TeamHistoryEntry(Guid MatchId, string MatchName, int Score, DateTime PlayedAtUtc);

public sealed record TeamHistoryResponse(IReadOnlyList<TeamHistoryEntry> Matches);
