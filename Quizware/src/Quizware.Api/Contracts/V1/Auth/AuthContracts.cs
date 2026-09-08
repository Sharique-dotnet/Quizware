namespace Quizware.Api.Contracts.V1.Auth;

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<MeProgramMembership> Programs);

public sealed record MeProgramMembership(Guid ProgramId, string ProgramName, IReadOnlyList<string> RolesInProgram);

public sealed record SelectProgramRequest(Guid ProgramId);

public sealed record DisplayTokenRequest(Guid ProgramId, int ExpiresInMinutes = 720);

public sealed record DisplayTokenResponse(string DisplayToken, DateTime ExpiresAtUtc);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LogoutRequest(string RefreshToken);
