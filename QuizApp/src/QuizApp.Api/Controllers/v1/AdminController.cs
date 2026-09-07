using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Admin;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", utcNow = DateTime.UtcNow });

    [HttpGet("users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public ActionResult<AdminUsersResponse> ListUsers() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("users")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public ActionResult<AdminUserSummaryDto> InviteUser([FromBody] InviteUserRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public ActionResult<AdminUserSummaryDto> AssignRoles(Guid id, [FromBody] AssignUserRolesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("users/{id:guid}/deactivate")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin}")]
    public IActionResult DeactivateUser(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("lookups")]
    [Authorize]
    public ActionResult<LookupsResponse> Lookups() => StatusCode(StatusCodes.Status501NotImplemented);
}
