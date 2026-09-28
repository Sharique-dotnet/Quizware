using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;
using Quizware.Application.Gameplay.Commands;
using Quizware.Application.Gameplay.Queries;
using App = Quizware.Application.Gameplay.Dtos;

namespace Quizware.Api.Controllers.v1;

/// <summary>The match engine (05-API-Design.md §5.3). The match id alone
/// identifies the match — the tenant query filter keeps it inside the caller's
/// program. Snapshot/restore are not implemented yet and still return 501.</summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/live")]
[Authorize]
public sealed class LiveMatchController : ControllerBase
{
    private readonly ISender _sender;

    public LiveMatchController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("state")]
    [Authorize(Policy = Policies.CanViewLive)]
    public async Task<ActionResult<LiveMatchStateResponse>> GetState(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new GetLiveMatchStateQuery(matchId), cancellationToken)));

    [HttpPost("start")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<StartMatchResponse>> Start(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new StartMatchCommand(matchId), cancellationToken)));

    [HttpPost("pause")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> Pause(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new PauseMatchCommand(matchId), cancellationToken)));

    [HttpPost("resume")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> Resume(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new ResumeMatchCommand(matchId), cancellationToken)));

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
    public async Task<ActionResult<LiveMatchStateResponse>> End(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new EndMatchCommand(matchId), cancellationToken)));

    [HttpPost("abandon")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public async Task<ActionResult<LiveMatchStateResponse>> Abandon(
        Guid matchId, [FromBody] AbandonMatchRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new AbandonMatchCommand(matchId, request.Reason), cancellationToken)));

    [HttpGet("timeline")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Operator},{Roles.Auditor}")]
    public async Task<ActionResult<MatchTimelineResponse>> Timeline(Guid matchId, CancellationToken cancellationToken)
    {
        var events = await _sender.Send(new GetMatchTimelineQuery(matchId), cancellationToken);
        return Ok(new MatchTimelineResponse(
            events.Select(e => new MatchEventDto(e.SequenceNumber, e.EventType, e.Detail, e.OccurredAtUtc)).ToList()));
    }

    [HttpPost("snapshot")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<MatchSnapshotResponse> Snapshot(Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("restore/{snapshotId:guid}")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public ActionResult<LiveMatchStateResponse> Restore(Guid matchId, Guid snapshotId) => StatusCode(StatusCodes.Status501NotImplemented);
}
