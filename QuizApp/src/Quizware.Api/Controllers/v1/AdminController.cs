using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Admin;
using Quizware.Application.Authorization;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly AppDbContext _dbContext;

    public AdminController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, AppDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", utcNow = DateTime.UtcNow });

    [HttpGet("users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<ActionResult<AdminUsersResponse>> ListUsers(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        var summaries = new List<AdminUserSummaryDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            summaries.Add(new AdminUserSummaryDto(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, roles.ToList()));
        }

        return Ok(new AdminUsersResponse(summaries));
    }

    [HttpPost("users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<ActionResult<InviteUserResponse>> InviteUser([FromBody] InviteUserRequest request)
    {
        if (!request.Roles.All(Roles.All.Contains))
        {
            ModelState.AddModelError(nameof(request.Roles), "One or more roles are not recognised.");
            return ValidationProblem(ModelState);
        }

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Conflict();
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true,
            EmailConfirmed = true,
        };

        var created = await _userManager.CreateAsync(user, temporaryPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        if (request.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        var summary = new AdminUserSummaryDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles);
        return CreatedAtAction(nameof(ListUsers), null, new InviteUserResponse(summary, temporaryPassword));
    }

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<ActionResult<AssignUserRolesResponse>> AssignRoles(
        Guid id, [FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken)
    {
        if (!request.Roles.All(Roles.All.Contains))
        {
            ModelState.AddModelError(nameof(request.Roles), "One or more roles are not recognised.");
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!await _dbContext.Programs.AnyAsync(p => p.Id == request.ProgramId, cancellationToken))
        {
            return NotFound();
        }

        var existing = await _dbContext.ProgramUsers
            .Where(pu => pu.UserId == id && pu.ProgramId == request.ProgramId && pu.IsActive && !pu.IsDeleted)
            .ToListAsync(cancellationToken);

        var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "admin";
        var now = DateTime.UtcNow;

        var roleIdsByName = new Dictionary<string, Guid>();
        foreach (var roleName in request.Roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                roleIdsByName[roleName] = role.Id;
            }
        }

        foreach (var row in existing)
        {
            if (!roleIdsByName.Values.Contains(row.RoleId))
            {
                row.IsDeleted = true;
                row.DeletedAtUtc = now;
                row.UpdatedAtUtc = now;
                row.UpdatedBy = actor;
            }
        }

        var existingRoleIds = existing.Where(r => !r.IsDeleted).Select(r => r.RoleId).ToHashSet();
        foreach (var (roleName, roleId) in roleIdsByName)
        {
            if (!existingRoleIds.Contains(roleId))
            {
                _dbContext.ProgramUsers.Add(new ProgramUser
                {
                    Id = Guid.NewGuid(),
                    ProgramId = request.ProgramId,
                    UserId = id,
                    RoleId = roleId,
                    IsActive = true,
                    CreatedAtUtc = now,
                    CreatedBy = actor,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AssignUserRolesResponse(id, request.ProgramId, roleIdsByName.Keys.ToList()));
    }

    [HttpPost("users/{id:guid}/deactivate")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("users/{id:guid}/reset-password")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var temporaryPassword = GenerateTemporaryPassword();
        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, temporaryPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new ResetPasswordResponse(temporaryPassword));
    }

    [HttpGet("lookups")]
    [Authorize]
    public ActionResult<LookupsResponse> Lookups() => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Satisfies the configured Identity password policy
    /// (upper/lower/digit/non-alphanumeric) regardless of length settings.</summary>
    private static string GenerateTemporaryPassword() =>
        $"Tmp-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}!1";
}
