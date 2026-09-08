using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Stages;
using QuizApp.Api.Contracts.V1.Standings;
using QuizApp.Application.Authorization;
using QuizApp.Application.Tournament.Commands;
using QuizApp.Application.Tournament.Dtos;
using QuizApp.Application.Tournament.Queries;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/stages")]
[Authorize]
public sealed class StagesController : ControllerBase
{
    private readonly ISender _sender;

    public StagesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StageSummaryResponse>>> List(Guid programId, CancellationToken cancellationToken)
    {
        var stages = await _sender.Send(new ListStagesQuery(programId), cancellationToken);
        return Ok(stages.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StageDetailResponse>> GetById(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var stage = await _sender.Send(new GetStageByIdQuery(programId, id), cancellationToken);
        return Ok(ToDetailResponse(stage));
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageDetailResponse>> Create(
        Guid programId, [FromBody] CreateStageRequest request, CancellationToken cancellationToken)
    {
        var stage = await _sender.Send(
            new CreateStageCommand(programId, request.Name, request.OrderIndex, request.StageType), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { programId, id = stage.Id }, ToDetailResponse(stage));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageDetailResponse>> Update(
        Guid programId, Guid id, [FromBody] UpdateStageRequest request, CancellationToken cancellationToken)
    {
        var stage = await _sender.Send(new UpdateStageCommand(programId, id, request.Name), cancellationToken);
        return Ok(ToDetailResponse(stage));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> Delete(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteStageCommand(programId, id), cancellationToken);
        return NoContent();
    }

    [HttpPut("reorder")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<IReadOnlyList<StageSummaryResponse>>> Reorder(
        Guid programId, [FromBody] ReorderStagesRequest request, CancellationToken cancellationToken)
    {
        var stages = await _sender.Send(new ReorderStagesCommand(programId, request.OrderedStageIds), cancellationToken);
        return Ok(stages.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<ActionResult<IReadOnlyList<StageSegmentTemplateDto>>> GetSegments(
        Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var segments = await _sender.Send(new ListSegmentsQuery(programId, id), cancellationToken);
        return Ok(segments.Select(ToSegmentResponse).ToList());
    }

    [HttpPost("{id:guid}/segments")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageSegmentTemplateDto>> AddSegment(
        Guid programId, Guid id, [FromBody] CreateSegmentTemplateRequest request, CancellationToken cancellationToken)
    {
        var segment = await _sender.Send(
            new CreateSegmentTemplateCommand(programId, id, request.FormatCode, request.QuestionCount, request.IsOrderLocked),
            cancellationToken);
        return Ok(ToSegmentResponse(segment));
    }

    [HttpPut("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageSegmentTemplateDto>> UpdateSegment(
        Guid programId, Guid id, Guid segId, [FromBody] UpdateSegmentTemplateRequest request, CancellationToken cancellationToken)
    {
        var segment = await _sender.Send(
            new UpdateSegmentTemplateCommand(programId, id, segId, request.QuestionCount, request.IsOrderLocked),
            cancellationToken);
        return Ok(ToSegmentResponse(segment));
    }

    [HttpDelete("{id:guid}/segments/{segId:guid}")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<IActionResult> RemoveSegment(Guid programId, Guid id, Guid segId, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteSegmentTemplateCommand(programId, id, segId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/segments/reorder")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<ReorderSegmentTemplatesResponse>> ReorderSegments(
        Guid programId, Guid id, [FromBody] ReorderSegmentTemplatesRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReorderSegmentTemplatesCommand(programId, id, request.OrderedSegmentTemplateIds), cancellationToken);
        return Ok(new ReorderSegmentTemplatesResponse(
            result.StageId,
            result.SegmentOrderMode,
            result.Segments.Select(ToSegmentResponse).ToList(),
            new AffectedMatchesDto(result.AffectedMatchesNotYetCreated, result.AffectedMatchesAlreadyCreated, result.AffectedMatchesInProgress)));
    }

    [HttpPut("{id:guid}/segment-order-mode")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageDetailResponse>> SetSegmentOrderMode(
        Guid programId, Guid id, [FromBody] SetSegmentOrderModeRequest request, CancellationToken cancellationToken)
    {
        var stage = await _sender.Send(
            new SetSegmentOrderModeCommand(programId, id, request.SegmentOrderMode), cancellationToken);
        return Ok(ToDetailResponse(stage));
    }

    [HttpGet("{id:guid}/standings")]
    public ActionResult<StageStandingsResponse> Standings(Guid programId, Guid id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = Policies.CanManageProgram)]
    public async Task<ActionResult<StageValidationResponse>> Validate(Guid programId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ValidateStageQuery(programId, id), cancellationToken);
        return Ok(new StageValidationResponse(result.IsRunnable, result.Blockers));
    }

    private static StageSummaryResponse ToSummaryResponse(StageSummaryDto dto) =>
        new(dto.Id, dto.Name, dto.OrderIndex, dto.State);

    private static StageDetailResponse ToDetailResponse(StageDto dto) =>
        new(dto.Id, dto.Name, dto.OrderIndex, dto.State, dto.SegmentOrderMode, dto.Segments.Select(ToSegmentResponse).ToList());

    private static StageSegmentTemplateDto ToSegmentResponse(Application.Tournament.Dtos.SegmentTemplateDto dto) =>
        new(dto.Id, dto.FormatCode, dto.OrderIndex, dto.QuestionCount, dto.IsOrderLocked);
}
