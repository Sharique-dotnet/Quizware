namespace QuizApp.Api.Contracts.V1.Admin;

public sealed record AdminUserSummaryDto(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles);

public sealed record AdminUsersResponse(IReadOnlyList<AdminUserSummaryDto> Users);

public sealed record InviteUserRequest(string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AssignUserRolesRequest(Guid ProgramId, IReadOnlyList<string> Roles);

public sealed record LookupEntry(string Code, string DisplayName);

public sealed record LookupsResponse(IReadOnlyDictionary<string, IReadOnlyList<LookupEntry>> Lookups);
