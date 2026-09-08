using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/matches")]
[Authorize]
public sealed class MatchesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<MatchSummaryResponse>> List(Guid programId, [FromQuery] Guid? stageId, [FromQuery] string? state) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    public ActionResult<MatchDetailResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<MatchDetailResponse> Create(Guid programId, [FromBody] CreateMatchRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<MatchDetailResponse> Update(Guid programId, Guid id, [FromBody] UpdateMatchRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/participants")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<MatchParticipantDto> AddParticipant(Guid programId, Guid id, [FromBody] AddMatchParticipantRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}/participants/{pid:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult RemoveParticipant(Guid programId, Guid id, Guid pid) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/participants/order")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<IReadOnlyList<MatchParticipantDto>> OrderParticipants(Guid programId, Guid id, [FromBody] OrderMatchParticipantsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchSegmentsResponse> GetSegments(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/segments/reorder")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchSegmentsResponse> ReorderSegments(Guid programId, Guid id, [FromBody] ReorderMatchSegmentsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<MatchSegmentSummaryDto> AddSegment(Guid programId, Guid id, [FromBody] AddMatchSegmentRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult RemoveSegment(Guid programId, Guid id, Guid segId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/segments/reset-to-template")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<MatchSegmentsResponse> ResetSegmentsToTemplate(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/ready")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchReadyResponse> Ready(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("auto-seed")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<AutoSeedMatchesResponse> AutoSeed(Guid programId, [FromBody] AutoSeedMatchesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/preflight")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchPreflightResponse> Preflight(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
