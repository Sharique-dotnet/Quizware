using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Standings;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/standings")]
[Authorize]
public sealed class StandingsController : ControllerBase
{
    [HttpGet("overall")]
    public ActionResult<OverallStandingsResponse> Overall(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("stages/{stageId:guid}")]
    public ActionResult<StageStandingsResponse> Stage(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("teams/{teamId:guid}")]
    public ActionResult<TeamStandingResponse> Team(Guid programId, Guid teamId) => StatusCode(StatusCodes.Status501NotImplemented);
}
