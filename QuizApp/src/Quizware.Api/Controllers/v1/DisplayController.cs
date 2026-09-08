using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Display;
using Quizware.Application.Authorization;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/display")]
[Authorize(Policy = Policies.DisplayOnly)]
public sealed class DisplayController : ControllerBase
{
    [HttpGet("matches/{matchId:guid}/state")]
    public ActionResult<DisplayMatchStateResponse> MatchState(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("matches/{matchId:guid}/scores")]
    public ActionResult<DisplayScoresResponse> MatchScores(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("programs/{programId:guid}/standings")]
    public ActionResult<DisplayStandingsResponse> Standings(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("programs/{programId:guid}/branding")]
    public ActionResult<DisplayBrandingResponse> Branding(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);
}
