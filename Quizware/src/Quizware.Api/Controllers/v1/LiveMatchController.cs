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
    public async Task<ActionResult<LiveMatchStateResponse>> OpenSegment(Guid matchId, Guid segId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new OpenSegmentCommand(matchId, segId), cancellationToken)));

    [HttpPost("segments/{segId:guid}/close")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> CloseSegment(Guid matchId, Guid segId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new CloseSegmentCommand(matchId, segId), cancellationToken)));

    [HttpPost("segments/{segId:guid}/skip")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> SkipSegment(
        Guid matchId, Guid segId, [FromBody] SkipSegmentRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new SkipSegmentCommand(matchId, segId, request.Reason), cancellationToken)));

    [HttpPut("segments/reorder")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<MatchSegmentsResponse>> ReorderSegments(
        Guid matchId, [FromBody] ReorderMatchSegmentsRequest request, CancellationToken cancellationToken) =>
        Ok(ToSegmentsResponse(await _sender.Send(
            new ReorderMatchSegmentsCommand(null, matchId, request.OrderedSegmentIds, request.Reason), cancellationToken)));

    [HttpGet("segments/next-options")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<MatchSegmentsResponse>> NextSegmentOptions(Guid matchId, CancellationToken cancellationToken) =>
        Ok(ToSegmentsResponse(await _sender.Send(new GetNextSegmentOptionsQuery(matchId), cancellationToken)));

    [HttpGet("next-question")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<CurrentQuestionDto>> NextQuestion(Guid matchId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new PeekNextQuestionQuery(matchId), cancellationToken)));

    [HttpPost("questions/serve")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<CurrentQuestionDto>> ServeQuestion(
        Guid matchId, [FromBody] ServeQuestionRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new ServeQuestionCommand(matchId, request.SegmentId), cancellationToken)));

    [HttpGet("questions/{mqId:guid}")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<CurrentQuestionDto>> GetQuestion(Guid matchId, Guid mqId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new GetMatchQuestionQuery(matchId, mqId), cancellationToken)));

    [HttpPost("questions/{mqId:guid}/reveal")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<CurrentQuestionDto>> RevealQuestion(Guid matchId, Guid mqId, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new RevealQuestionCommand(matchId, mqId), cancellationToken)));

    [HttpPost("questions/{mqId:guid}/skip")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> SkipQuestion(
        Guid matchId, Guid mqId, [FromBody] SkipQuestionRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new SkipQuestionCommand(matchId, mqId, request.Reason), cancellationToken)));

    [HttpPost("answers")]
    [Authorize(Policy = Policies.CanRecordAnswer)]
    public async Task<ActionResult<RecordAnswerResponse>> RecordAnswer(
        Guid matchId,
        [FromBody] RecordAnswerRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RecordAnswerCommand(
                matchId, request.MatchQuestionId, request.MatchParticipantId, request.Outcome, request.SelectedOptionId,
                request.SelectedOptionIds, request.FreeTextAnswer, request.PassNumber, request.AnswerSource, request.BuzzPressId,
                request.ResponseTimeMs, idempotencyKey),
            cancellationToken);
        return Ok(LiveMapper.ToResponse(result));
    }

    [HttpPost("answers/{id:guid}/reverse")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<RecordAnswerResponse>> ReverseAnswer(
        Guid matchId, Guid id, [FromBody] ReverseAnswerRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new ReverseAnswerCommand(matchId, id, request.Reason), cancellationToken)));

    [HttpPost("pass")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> Pass(
        Guid matchId, [FromBody] PassQuestionRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(
            new PassQuestionCommand(matchId, request.MatchQuestionId, request.FromParticipantId), cancellationToken)));

    [HttpPost("topics/select")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<LiveMatchStateResponse>> SelectTopic(
        Guid matchId, [FromBody] SelectTopicRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new SelectTopicCommand(matchId, request.ParticipantId, request.TopicName), cancellationToken)));

    [HttpGet("topics/available")]
    [Authorize(Policy = Policies.CanViewLive)]
    public async Task<ActionResult<AvailableTopicsResponse>> AvailableTopics(Guid matchId, CancellationToken cancellationToken)
    {
        var topics = await _sender.Send(new GetAvailableTopicsQuery(matchId), cancellationToken);
        return Ok(new AvailableTopicsResponse(topics.Topics, topics.TopicChoiceLimit));
    }

    [HttpPost("participants/{pid:guid}/disqualify")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public async Task<ActionResult<DisqualifyParticipantResponse>> Disqualify(
        Guid matchId, Guid pid, [FromBody] DisqualifyParticipantRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(
            new DisqualifyParticipantCommand(matchId, pid, request.Reason, request.ApprovedByUserId, request.ExcludeFromStandings),
            cancellationToken)));

    [HttpPost("participants/{pid:guid}/reinstate")]
    [Authorize(Policy = Policies.CanDisqualify)]
    public async Task<ActionResult<LiveMatchStateResponse>> Reinstate(
        Guid matchId, Guid pid, [FromBody] ReinstateParticipantRequest request, CancellationToken cancellationToken) =>
        Ok(LiveMapper.ToResponse(await _sender.Send(new ReinstateParticipantCommand(matchId, pid, request.Reason), cancellationToken)));

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

    private static MatchSegmentsResponse ToSegmentsResponse(IReadOnlyList<App.MatchSegmentDto> segments) =>
        new(segments.Select(s => new MatchSegmentSummaryDto(s.Id, s.FormatCode, s.OrderIndex, s.State, s.IsOrderLocked)).ToList());
}
