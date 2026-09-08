using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Application.Authorization;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/qualification")]
[Authorize(Policy = Policies.CanManageProgram)]
public sealed class QualificationController : ControllerBase
{
    [HttpGet("stages/{stageId:guid}/preview")]
    public ActionResult<QualificationPreviewResponse> Preview(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("stages/{stageId:guid}/wildcards")]
    public ActionResult<QualificationPreviewResponse> SetWildcards(Guid programId, Guid stageId, [FromBody] SetWildcardsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("stages/{stageId:guid}/commit")]
    public ActionResult<CommitQualificationResponse> Commit(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("stages/{stageId:guid}/rollback")]
    public IActionResult Rollback(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("stages/{stageId:guid}/ties")]
    public ActionResult<TiesResponse> GetTies(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("stages/{stageId:guid}/ties/detect")]
    public ActionResult<TiesResponse> DetectTies(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("ties/{tieId:guid}")]
    public ActionResult<TieDto> GetTie(Guid programId, Guid tieId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("ties/{tieId:guid}/tie-break-match")]
    public ActionResult<CreateTieBreakMatchResponse> CreateTieBreakMatch(Guid programId, Guid tieId, [FromBody] CreateTieBreakMatchRequest? request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("ties/{tieId:guid}/resolve-manually")]
    [Authorize(Policy = Policies.CanResolveTie)]
    public ActionResult<TieDto> ResolveManually(Guid programId, Guid tieId, [FromBody] ResolveTieManuallyRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("ties/{tieId:guid}/abandon")]
    public IActionResult AbandonTie(Guid programId, Guid tieId, [FromBody] AbandonTieRequest request) => StatusCode(StatusCodes.Status501NotImplemented);
}
