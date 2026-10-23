using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Application.Authorization;
using Quizware.Application.Buzzer;

namespace Quizware.Api.Controllers.v1;

/// <summary>Optional module (ADR-005). With no provider configured, or with
/// the buzzer module deleted, everything that needs hardware returns
/// <c>503 BUZZER_UNAVAILABLE</c> — never a 500, and gameplay is never blocked:
/// answers are recorded the same way, with <c>answerSource = Operator</c>.
/// Capability and health always answer, so the console can decide to show
/// manual buttons.</summary>
[ApiController]
[Route("api/v1/buzzer")]
[Authorize]
public sealed class BuzzerController : ControllerBase
{
    private readonly ISender _sender;

    public BuzzerController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("capability")]
    [Authorize(Policy = Policies.CanViewLive)]
    public async Task<ActionResult<BuzzerCapabilityResponse>> Capability(CancellationToken cancellationToken)
    {
        var capability = await _sender.Send(new GetBuzzerCapabilityQuery(), cancellationToken);
        return Ok(new BuzzerCapabilityResponse(capability.Available, capability.Provider, capability.DeviceCount));
    }

    [HttpGet("health")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<BuzzerHealthResponse>> Health(CancellationToken cancellationToken)
    {
        var health = await _sender.Send(new GetBuzzerHealthQuery(), cancellationToken);
        var capability = await _sender.Send(new GetBuzzerCapabilityQuery(), cancellationToken);
        return Ok(new BuzzerHealthResponse(health.Healthy, health.Detail, capability.Provider, health.Devices.Select(ToDto).ToList()));
    }

    [HttpGet("devices")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<BuzzerDevicesResponse>> Devices(CancellationToken cancellationToken)
    {
        var health = await _sender.Send(new GetBuzzerHealthQuery(), cancellationToken);
        return Ok(new BuzzerDevicesResponse(health.Devices.Select(ToDto).ToList()));
    }

    [HttpPut("matches/{matchId:guid}/mappings")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> SetMappings(Guid matchId, [FromBody] SetDeviceMappingsRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(
            new SetDeviceMappingsCommand(matchId, request.Mappings.Select(m => new DeviceMappingInput(m.DeviceId, m.MatchParticipantId)).ToList()),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("matches/{matchId:guid}/sessions")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<ArmBuzzSessionResponse>> ArmSession(Guid matchId, [FromBody] ArmBuzzSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _sender.Send(new ArmBuzzSessionCommand(matchId, request.SegmentId, request.MatchQuestionId), cancellationToken);
        return Ok(new ArmBuzzSessionResponse(session.SessionId, session.State));
    }

    [HttpGet("sessions/{id:guid}")]
    [Authorize(Policy = Policies.CanViewLive)]
    public async Task<ActionResult<BuzzSessionResponse>> GetSession(Guid id, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(new GetBuzzSessionQuery(id), cancellationToken)));

    /// <summary>Pushed by the on-premises buzzer agent (ADR-005), not by a
    /// browser client. The first press per device counts; a repeat is ignored.</summary>
    [HttpPost("sessions/{id:guid}/presses")]
    [AllowAnonymous]
    public async Task<ActionResult<BuzzSessionResponse>> SubmitPress(Guid id, [FromBody] SubmitBuzzPressRequest request, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(new SubmitBuzzPressCommand(id, request.DeviceId, request.ElapsedMs, request.RawFrameHex), cancellationToken)));

    [HttpPost("sessions/{id:guid}/collect")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<BuzzSessionResponse>> Collect(Guid id, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(new CollectBuzzSessionCommand(id), cancellationToken)));

    [HttpPost("sessions/{id:guid}/reset")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<IActionResult> ResetSession(Guid id, [FromBody] ResetBuzzSessionRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new ResetBuzzSessionCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("test")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<BuzzerTestResponse>> Test(CancellationToken cancellationToken)
    {
        var readings = await _sender.Send(new TestBuzzersCommand(), cancellationToken);
        return Ok(new BuzzerTestResponse(readings
            .Select(r => new BuzzerDeviceDto(r.DeviceId, null, r.IsOnline, r.ButtonTimesMs, r.RawFrameHex))
            .ToList()));
    }

    private static BuzzerDeviceDto ToDto(BuzzerDeviceStatus device) => new(device.DeviceId, device.Port, device.IsOnline);

    private static BuzzSessionResponse ToResponse(BuzzSessionDto dto) =>
        new(
            dto.SessionId,
            dto.State,
            dto.RankedPresses
                .Select(p => new BuzzPressDto(
                    p.DeviceId, p.MatchParticipantId, p.ElapsedMs, p.Rank, p.PressId, p.TeamId, p.TeamName, p.ButtonKey, p.RawFrameHex, p.ReceivedAtUtc))
                .ToList(),
            dto.MatchId,
            dto.MatchQuestionId,
            dto.Provider,
            dto.WindowMs,
            dto.FailureReason,
            dto.ArmedAtUtc,
            dto.CollectedAtUtc);
}
