using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Application.Authorization;

namespace Quizware.Api.Controllers.v1;

/// <summary>Optional module (ADR-005). Every action returns 501 in this
/// contract-only phase; once implemented (Phase 14), an offline/absent
/// provider returns 503 BUZZER_UNAVAILABLE instead — never a 500, and it
/// never blocks gameplay.</summary>
[ApiController]
[Route("api/v1/buzzer")]
[Authorize]
public sealed class BuzzerController : ControllerBase
{
    [HttpGet("capability")]
    [Authorize(Policy = Policies.CanViewLive)]
    public ActionResult<BuzzerCapabilityResponse> Capability() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("health")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<BuzzerHealthResponse> Health() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("devices")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<BuzzerDevicesResponse> Devices() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("matches/{matchId:guid}/mappings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult SetMappings(Guid matchId, [FromBody] SetDeviceMappingsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("matches/{matchId:guid}/sessions")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<ArmBuzzSessionResponse> ArmSession(Guid matchId, [FromBody] ArmBuzzSessionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("sessions/{id:guid}")]
    [Authorize(Policy = Policies.CanViewLive)]
    public ActionResult<BuzzSessionResponse> GetSession(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Pushed by the on-premises buzzer agent (ADR-005), not by a
    /// browser client — it will carry its own agent-to-API credential once
    /// P14 implements the adapters, not a user JWT. Left anonymous here as a
    /// deliberate Phase 5 scope decision; do not treat this as the final
    /// security posture.</summary>
    [HttpPost("sessions/{id:guid}/presses")]
    [AllowAnonymous]
    public IActionResult SubmitPress(Guid id, [FromBody] SubmitBuzzPressRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("sessions/{id:guid}/collect")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<BuzzSessionResponse> Collect(Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("sessions/{id:guid}/reset")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public IActionResult ResetSession(Guid id, [FromBody] ResetBuzzSessionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("test")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<BuzzerTestResponse> Test() => StatusCode(StatusCodes.Status501NotImplemented);
}
