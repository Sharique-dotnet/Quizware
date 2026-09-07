namespace QuizApp.Infrastructure.Identity;

public interface IJwtTokenService
{
    /// <summary>Issues a 15-minute access token. <paramref name="programId"/>
    /// becomes the `program_id` claim that ADR-002's tenant filter trusts.</summary>
    string GenerateAccessToken(AppUser user, IEnumerable<string> roles, Guid? programId);

    /// <summary>Issues a new opaque refresh token (7 days) and its SHA-256
    /// hash — only the hash is ever persisted.</summary>
    (string Token, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashRefreshToken(string token);
}
