using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;
using Quizware.Application.Gameplay.Commands;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Queries;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/matches")]
[Authorize]
public sealed class MatchesController : ControllerBase
{
    private readonly ISender _sender;

    public MatchesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchSummaryResponse>>> List(
        Guid programId, [FromQuery] Guid? stageId, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var matches = await _sender.Send(new ListMatchesQuery(programId, stageId, state), cancellationToken);
        return Ok(matches.Select(m => new MatchSummaryResponse(m.Id, m.Name, m.StageId, m.State, m.MatchNumber)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDetailResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var match = await _sender.Send(new GetMatchByIdQuery(programId, id), cancellationToken);
        return Ok(ToDetailResponse(match));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<MatchDetailResponse>> Create(
        Guid programId, [FromBody] CreateMatchRequest request, CancellationToken cancellationToken)
    {
        var match = await _sender.Send(
            new CreateMatchCommand(programId, request.StageId, request.Name, request.MatchNumber), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = match.Id }, ToDetailResponse(match));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<MatchDetailResponse>> Update(
        Guid programId, Guid id, [FromBody] UpdateMatchRequest request, CancellationToken cancellationToken)
    {
        var match = await _sender.Send(
            new UpdateMatchCommand(programId, id, request.Name, request.MatchNumber), cancellationToken);
        return Ok(ToDetailResponse(match));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteMatchCommand(programId, id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/participants")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<MatchParticipantDto>> AddParticipant(
        Guid programId, Guid id, [FromBody] AddMatchParticipantRequest request, CancellationToken cancellationToken)
    {
        var participant = await _sender.Send(
            new AddMatchParticipantCommand(programId, id, request.TeamId, request.SeatNumber), cancellationToken);
        return Ok(ToParticipantResponse(participant));
    }

    [HttpDelete("{id:guid}/participants/{pid:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> RemoveParticipant(Guid programId, Guid id, Guid pid, CancellationToken cancellationToken)
    {
        await _sender.Send(new RemoveMatchParticipantCommand(programId, id, pid), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/participants/order")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<IReadOnlyList<MatchParticipantDto>>> OrderParticipants(
        Guid programId, Guid id, [FromBody] OrderMatchParticipantsRequest request, CancellationToken cancellationToken)
    {
        var entries = request.Participants
            .Select(p => new ParticipantOrderEntry(p.ParticipantId, p.SeatNumber, p.TurnOrder))
            .ToList();
        var participants = await _sender.Send(new OrderMatchParticipantsCommand(programId, id, entries), cancellationToken);
        return Ok(participants.Select(ToParticipantResponse).ToList());
    }

    [HttpGet("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<MatchSegmentsResponse>> GetSegments(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var segments = await _sender.Send(new GetMatchSegmentsQuery(programId, id), cancellationToken);
        return Ok(ToSegmentsResponse(segments));
    }

    [HttpPut("{id:guid}/segments/reorder")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<MatchSegmentsResponse>> ReorderSegments(
        Guid programId, Guid id, [FromBody] ReorderMatchSegmentsRequest request, CancellationToken cancellationToken)
    {
        var segments = await _sender.Send(
            new ReorderMatchSegmentsCommand(programId, id, request.OrderedSegmentIds, request.Reason), cancellationToken);
        return Ok(ToSegmentsResponse(segments));
    }

    [HttpPost("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<MatchSegmentSummaryDto>> AddSegment(
        Guid programId, Guid id, [FromBody] AddMatchSegmentRequest request, CancellationToken cancellationToken)
    {
        var segment = await _sender.Send(
            new AddMatchSegmentCommand(programId, id, request.FormatCode, request.QuestionCount), cancellationToken);
        return Ok(ToSegmentResponse(segment));
    }

    [HttpDelete("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> RemoveSegment(Guid programId, Guid id, Guid segId, CancellationToken cancellationToken)
    {
        await _sender.Send(new RemoveMatchSegmentCommand(programId, id, segId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/segments/reset-to-template")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<MatchSegmentsResponse>> ResetSegmentsToTemplate(
        Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var segments = await _sender.Send(new ResetMatchSegmentsToTemplateCommand(programId, id), cancellationToken);
        return Ok(ToSegmentsResponse(segments));
    }

    [HttpPost("{id:guid}/ready")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public async Task<ActionResult<MatchReadyResponse>> Ready(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkMatchReadyCommand(programId, id), cancellationToken);
        return Ok(new MatchReadyResponse(result.IsReady, result.Blockers));
    }

    [HttpPost("auto-seed")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public ActionResult<AutoSeedMatchesResponse> AutoSeed(Guid programId, [FromBody] AutoSeedMatchesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{id:guid}/preflight")]
    [Authorize(Policy = Policies.CanOperateMatch)]
    public ActionResult<MatchPreflightResponse> Preflight(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    private static MatchDetailResponse ToDetailResponse(MatchDto dto) =>
        new(dto.Id, dto.Name, dto.StageId, dto.State, dto.MatchNumber, dto.MatchKind,
            dto.Participants.Select(ToParticipantResponse).ToList());

    private static MatchParticipantDto ToParticipantResponse(ParticipantDto dto) =>
        new(dto.Id, dto.TeamId, dto.TeamName, dto.SeatNumber, dto.TurnOrder, dto.Status);

    private static MatchSegmentSummaryDto ToSegmentResponse(MatchSegmentDto dto) =>
        new(dto.Id, dto.FormatCode, dto.OrderIndex, dto.State, dto.IsOrderLocked);

    private static MatchSegmentsResponse ToSegmentsResponse(IReadOnlyList<MatchSegmentDto> segments) =>
        new(segments.Select(ToSegmentResponse).ToList());
}
