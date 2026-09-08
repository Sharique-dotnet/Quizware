namespace QuizApp.Api.Contracts.V1.Admin;

public sealed record AdminUserSummaryDto(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles);

public sealed record AdminUsersResponse(IReadOnlyList<AdminUserSummaryDto> Users);

public sealed record InviteUserRequest(string Email, string FullName, IReadOnlyList<string> Roles);

/// <summary>No email infrastructure exists in this codebase — the generated
/// temporary password is returned directly to the inviting admin, the same
/// trust boundary as the seeded default admin account.</summary>
public sealed record InviteUserResponse(AdminUserSummaryDto User, string TemporaryPassword);

public sealed record AssignUserRolesRequest(Guid ProgramId, IReadOnlyList<string> Roles);

public sealed record AssignUserRolesResponse(Guid UserId, Guid ProgramId, IReadOnlyList<string> RolesInProgram);

public sealed record ResetPasswordResponse(string TemporaryPassword);

public sealed record LookupEntry(string Code, string DisplayName);

public sealed record LookupsResponse(IReadOnlyDictionary<string, IReadOnlyList<LookupEntry>> Lookups);
