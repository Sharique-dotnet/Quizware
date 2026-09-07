using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Persistence;

namespace QuizApp.Api.Controllers.v1;

public sealed record LoginRequest(string Email, string Password);

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc);

public sealed record RefreshRequest(string RefreshToken);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly AppDbContext _dbContext;

    public AuthController(UserManager<AppUser> userManager, IJwtTokenService tokenService, AppDbContext dbContext)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                type = "https://quizapp/errors/unauthenticated",
                title = "Invalid email or password",
                status = 401,
                errorCode = "UNAUTHENTICATED",
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await IssueTokenPairAsync(user, roles, cancellationToken);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (stored is null || !stored.IsActive)
        {
            return Unauthorized(new
            {
                type = "https://quizapp/errors/unauthenticated",
                title = "Refresh token is invalid or expired",
                status = 401,
                errorCode = "UNAUTHENTICATED",
            });
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        // Rotate: revoke the presented token, issue a brand new pair.
        stored.RevokedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        return await IssueTokenPairAsync(user, roles, cancellationToken);
    }

    private async Task<TokenResponse> IssueTokenPairAsync(AppUser user, IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user, roles, programId: null);
        var (refreshToken, refreshTokenHash, expiresAtUtc) = _tokenService.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(15));
    }
}
