using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.LiveMatch;
using QuizApp.Api.Contracts.V1.Matches;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

/// <summary>The match engine (05-API-Design.md §5.3). Contract-only in Phase
/// 5 — every action returns 501 until IMatchEngine is implemented in Phase 9.</summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/live")]
[Authorize]
public sealed class LiveMatchController : ControllerBase
{
    [HttpGet("state")]
    [Authorize(Policy = Policies.CanViewLive)]
    public ActionResult<LiveMatchStateResponse> GetState(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("start")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<StartMatchResponse> Start(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("pause")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> Pause(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("resume")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> Resume(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("segments/{segId:guid}/open")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> OpenSegment(Guid matchId, Guid segId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("segments/{segId:guid}/close")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> CloseSegment(Guid matchId, Guid segId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("segments/{segId:guid}/skip")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> SkipSegment(Guid matchId, Guid segId, [FromBody] SkipSegmentRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("segments/reorder")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchSegmentsResponse> ReorderSegments(Guid matchId, [FromBody] ReorderMatchSegmentsRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("segments/next-options")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchSegmentsResponse> NextSegmentOptions(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("next-question")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<CurrentQuestionDto> NextQuestion(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("questions/serve")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<CurrentQuestionDto> ServeQuestion(Guid matchId, [FromBody] ServeQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("questions/{mqId:guid}")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<CurrentQuestionDto> GetQuestion(Guid matchId, Guid mqId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("questions/{mqId:guid}/reveal")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<CurrentQuestionDto> RevealQuestion(Guid matchId, Guid mqId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("questions/{mqId:guid}/skip")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> SkipQuestion(Guid matchId, Guid mqId, [FromBody] SkipQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("answers")]
    [Authorize(Policy = Policies.CanRecordAnswer)]
    public ActionResult<RecordAnswerResponse> RecordAnswer(Guid matchId, [FromBody] RecordAnswerRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("answers/{id:guid}/reverse")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<RecordAnswerResponse> ReverseAnswer(Guid matchId, Guid id, [FromBody] ReverseAnswerRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("pass")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> Pass(Guid matchId, [FromBody] PassQuestionRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("topics/select")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> SelectTopic(Guid matchId, [FromBody] SelectTopicRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("topics/available")]
    [Authorize(Policy = Policies.CanViewLive)]
    public ActionResult<AvailableTopicsResponse> AvailableTopics(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("participants/{pid:guid}/disqualify")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<DisqualifyParticipantResponse> Disqualify(Guid matchId, Guid pid, [FromBody] DisqualifyParticipantRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("participants/{pid:guid}/reinstate")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<LiveMatchStateResponse> Reinstate(Guid matchId, Guid pid, [FromBody] ReinstateParticipantRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("end")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<LiveMatchStateResponse> End(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("abandon")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<LiveMatchStateResponse> Abandon(Guid matchId, [FromBody] AbandonMatchRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("timeline")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Operator},{Roles.Auditor}")]
    public ActionResult<MatchTimelineResponse> Timeline(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("snapshot")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<MatchSnapshotResponse> Snapshot(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("restore/{snapshotId:guid}")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<LiveMatchStateResponse> Restore(Guid matchId, Guid snapshotId) => StatusCode(StatusCodes.Status501NotImplemented);
}
