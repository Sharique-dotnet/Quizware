using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizApp.Api.Contracts.V1.Auth;
using QuizApp.Application.Authorization;
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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.UserId == userId, cancellationToken);

        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var globalRoles = await _userManager.GetRolesAsync(user);

        var memberships = await _dbContext.ProgramUsers
            .Where(pu => pu.UserId == userId && pu.IsActive && !pu.IsDeleted)
            .Join(_dbContext.Programs, pu => pu.ProgramId, p => p.Id, (pu, p) => new { pu.ProgramId, ProgramName = p.Name, pu.RoleId })
            .ToListAsync(cancellationToken);

        var roleNamesById = await _dbContext.Roles.ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, cancellationToken);

        var programs = memberships
            .GroupBy(m => new { m.ProgramId, m.ProgramName })
            .Select(g => new MeProgramMembership(
                g.Key.ProgramId,
                g.Key.ProgramName,
                g.Select(m => roleNamesById.GetValueOrDefault(m.RoleId, string.Empty)).Where(n => n.Length > 0).ToList()))
            .ToList();

        return Ok(new MeResponse(user.Id, user.Email ?? string.Empty, user.FullName, globalRoles.ToList(), programs));
    }

    [HttpPost("select-program")]
    [Authorize]
    public async Task<ActionResult<TokenResponse>> SelectProgram(
        [FromBody] SelectProgramRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await _dbContext.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            return NotFound();
        }

        var globalRoles = await _userManager.GetRolesAsync(user);

        // SuperAdmin manages every program without an explicit ProgramUser
        // row (Policies.CanManageProgram already treats it this way) — a
        // select-program token for SuperAdmin carries that role as-is.
        List<string> scopedRoles;
        if (globalRoles.Contains(Roles.SuperAdmin))
        {
            scopedRoles = [Roles.SuperAdmin];
        }
        else
        {
            var roleIds = await _dbContext.ProgramUsers
                .Where(pu => pu.UserId == userId && pu.ProgramId == request.ProgramId && pu.IsActive && !pu.IsDeleted)
                .Select(pu => pu.RoleId)
                .ToListAsync(cancellationToken);

            if (roleIds.Count == 0)
            {
                return Forbid();
            }

            var roleNamesById = await _dbContext.Roles
                .Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, cancellationToken);
            scopedRoles = roleIds.Select(id => roleNamesById.GetValueOrDefault(id, string.Empty)).Where(n => n.Length > 0).ToList();
        }

        return await IssueTokenPairAsync(user, scopedRoles, request.ProgramId, cancellationToken);
    }

    [HttpPost("display-token")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<DisplayTokenResponse>> DisplayToken(
        [FromBody] DisplayTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await _dbContext.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            return NotFound();
        }

        var expiresIn = TimeSpan.FromMinutes(request.ExpiresInMinutes);

        // The Display role is embedded regardless of the minting admin's
        // own roles — this, not a separate token-kind claim, is what makes
        // "cannot write anything" true: every write policy is RequireRole
        // over roles that never include Display.
        var token = _tokenService.GenerateAccessToken(user, [Roles.Display], request.ProgramId, expiresIn);

        return Ok(new DisplayTokenResponse(token, DateTime.UtcNow.Add(expiresIn)));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private Task<TokenResponse> IssueTokenPairAsync(AppUser user, IEnumerable<string> roles, CancellationToken cancellationToken) =>
        IssueTokenPairAsync(user, roles, programId: null, cancellationToken);

    private async Task<TokenResponse> IssueTokenPairAsync(
        AppUser user, IEnumerable<string> roles, Guid? programId, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user, roles, programId);
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
