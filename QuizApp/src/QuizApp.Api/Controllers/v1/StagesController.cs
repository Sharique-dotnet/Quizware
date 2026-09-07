using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Stages;
using QuizApp.Api.Contracts.V1.Standings;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/stages")]
[Authorize]
public sealed class StagesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<StageSummaryResponse>> List(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    public ActionResult<StageDetailResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageDetailResponse> Create(Guid programId, [FromBody] CreateStageRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageDetailResponse> Update(Guid programId, Guid id, [FromBody] UpdateStageRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("reorder")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<IReadOnlyList<StageSummaryResponse>> Reorder(Guid programId, [FromBody] ReorderStagesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/segments")]
    public ActionResult<IReadOnlyList<StageSegmentTemplateDto>> GetSegments(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageSegmentTemplateDto> AddSegment(Guid programId, Guid id, [FromBody] CreateSegmentTemplateRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageSegmentTemplateDto> UpdateSegment(Guid programId, Guid id, Guid segId, [FromBody] UpdateSegmentTemplateRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult RemoveSegment(Guid programId, Guid id, Guid segId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/segments/reorder")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<ReorderSegmentTemplatesResponse> ReorderSegments(Guid programId, Guid id, [FromBody] ReorderSegmentTemplatesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}/segment-order-mode")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageDetailResponse> SetSegmentOrderMode(Guid programId, Guid id, [FromBody] SetSegmentOrderModeRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/standings")]
    public ActionResult<StageStandingsResponse> Standings(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<StageValidationResponse> Validate(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
