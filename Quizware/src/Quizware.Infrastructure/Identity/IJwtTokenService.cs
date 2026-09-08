namespace Quizware.Infrastructure.Identity;

public interface IJwtTokenService
{
    /// <summary>Issues an access token, valid for <paramref name="expiresIn"/>
    /// (defaulting to the configured access-token lifetime).
    /// <paramref name="programId"/> becomes the `program_id` claim that
    /// ADR-002's tenant filter trusts. <paramref name="roles"/> is embedded
    /// as-is — it need not match the user's actual Identity roles, which is
    /// how a display token can carry only the Display role regardless of
    /// who minted it.</summary>
    string GenerateAccessToken(AppUser user, IEnumerable<string> roles, Guid? programId, TimeSpan? expiresIn = null);

    /// <summary>Issues a new opaque refresh token (7 days) and its SHA-256
    /// hash — only the hash is ever persisted.</summary>
    (string Token, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashRefreshToken(string token);
}
