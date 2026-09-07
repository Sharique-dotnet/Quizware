using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Scores;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/matches/{matchId:guid}/scores")]
[Authorize]
public sealed class ScoresController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CanViewLive)]
    public ActionResult<MatchScoresResponse> GetScores(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("events")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Operator},{Roles.Auditor}")]
    public ActionResult<ScoreEventsResponse> GetEvents(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("adjust")]
    [Authorize(Policy = Policies.CanAdjustScore)]
    public ActionResult<MatchScoresResponse> Adjust(Guid matchId, [FromBody] AdjustScoreRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("recalculate")]
    [Authorize(Policy = Policies.CanAdjustScore)]
    public ActionResult<RecalculateScoresResponse> Recalculate(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);
}
