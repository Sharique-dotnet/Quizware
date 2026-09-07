namespace QuizApp.Api.Contracts.V1.Teams;

public sealed record TeamSummaryResponse(Guid Id, string RegistrationCode, string SchoolName, string TeamName, string Status);

public sealed record TeamMemberDto(Guid Id, string FullName, string? Role);

public sealed record TeamDetailResponse(
    Guid Id,
    string RegistrationCode,
    string SchoolName,
    string TeamName,
    string Status,
    IReadOnlyList<TeamMemberDto> Members);

public sealed record CreateTeamRequest(string RegistrationCode, string SchoolName, string TeamName, IReadOnlyList<string> MemberNames);

public sealed record UpdateTeamRequest(string SchoolName, string TeamName);

public sealed record ChangeTeamStatusRequest(string Status, string Reason);

public sealed record TeamImportValidateResponse(Guid BatchId, int RowCount, int ValidRowCount, IReadOnlyList<string> Errors);

public sealed record TeamImportCommitResponse(int TeamsCreated);

public sealed record TeamHistoryEntry(Guid MatchId, string MatchName, int Score, DateTime PlayedAtUtc);

public sealed record TeamHistoryResponse(IReadOnlyList<TeamHistoryEntry> Matches);
