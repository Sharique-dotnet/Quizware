using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Teams;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/teams")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TeamSummaryResponse>> List(Guid programId, [FromQuery] string? status) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}")]
    public ActionResult<TeamDetailResponse> GetById(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamDetailResponse> Create(Guid programId, [FromBody] CreateTeamRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamDetailResponse> Update(Guid programId, Guid id, [FromBody] UpdateTeamRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public IActionResult Delete(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamDetailResponse> ChangeStatus(Guid programId, Guid id, [FromBody] ChangeTeamStatusRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/images")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamDetailResponse> UploadImages(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("import/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamImportValidateResponse> ImportValidate(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("import/{batchId:guid}/commit")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<TeamImportCommitResponse> ImportCommit(Guid programId, Guid batchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/history")]
    public ActionResult<TeamHistoryResponse> History(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);
}
