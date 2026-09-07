using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", utcNow = DateTime.UtcNow });
}
