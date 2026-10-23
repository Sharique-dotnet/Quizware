using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Application.Authorization;
using Quizware.Application.Qualification.Commands;
using Quizware.Application.Qualification.Queries;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/qualification")]
[Authorize(Policy = Policies.CanManageProgram)]
public sealed class QualificationController : ControllerBase
{
    private readonly ISender _sender;

    public QualificationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("stages/{stageId:guid}/preview")]
    public async Task<ActionResult<QualificationPreviewResponse>> Preview(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(new GetQualificationPreviewQuery(programId, stageId), cancellationToken)));

    [HttpPost("stages/{stageId:guid}/wildcards")]
    public async Task<ActionResult<QualificationPreviewResponse>> SetWildcards(
        Guid programId, Guid stageId, [FromBody] SetWildcardsRequest request, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(new SetWildcardsCommand(programId, stageId, request.TeamIds), cancellationToken)));

    [HttpPost("stages/{stageId:guid}/commit")]
    public async Task<ActionResult<CommitQualificationResponse>> Commit(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(new CommitQualificationResponse(await _sender.Send(new CommitQualificationCommand(programId, stageId), cancellationToken)));

    [HttpPost("stages/{stageId:guid}/rollback")]
    public async Task<IActionResult> Rollback(Guid programId, Guid stageId, CancellationToken cancellationToken)
    {
        await _sender.Send(new RollbackQualificationCommand(programId, stageId), cancellationToken);
        return NoContent();
    }

    [HttpGet("stages/{stageId:guid}/ties")]
    public async Task<ActionResult<TiesResponse>> GetTies(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(new GetStageTiesQuery(programId, stageId), cancellationToken)));

    [HttpPost("stages/{stageId:guid}/ties/detect")]
    public async Task<ActionResult<TiesResponse>> DetectTies(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(new DetectTiesCommand(programId, stageId), cancellationToken)));

    [HttpGet("ties/{tieId:guid}")]
    public async Task<ActionResult<TieDto>> GetTie(Guid programId, Guid tieId, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(new GetTieQuery(programId, tieId), cancellationToken)));

    [HttpPost("ties/{tieId:guid}/tie-break-match")]
    public async Task<ActionResult<CreateTieBreakMatchResponse>> CreateTieBreakMatch(
        Guid programId, Guid tieId, [FromBody] CreateTieBreakMatchRequest? request, CancellationToken cancellationToken)
    {
        var created = await _sender.Send(
            new CreateTieBreakMatchCommand(
                programId, tieId, request?.FormatCodeOverride, request?.QuestionCountOverride, request?.SuddenDeathOverride, request?.Reason),
            cancellationToken);
        return Created($"/api/v1/programs/{programId}/matches/{created.MatchId}", QualificationMapper.ToResponse(created));
    }

    [HttpPost("ties/{tieId:guid}/resolve-manually")]
    [Authorize(Policy = Policies.CanResolveTie)]
    public async Task<ActionResult<TieDto>> ResolveManually(
        Guid programId, Guid tieId, [FromBody] ResolveTieManuallyRequest request, CancellationToken cancellationToken) =>
        Ok(QualificationMapper.ToResponse(await _sender.Send(
            new ResolveTieManuallyCommand(programId, tieId, request.RankedTeamIds, request.Method, request.Reason, request.ApprovedByUserId),
            cancellationToken)));

    [HttpPost("ties/{tieId:guid}/abandon")]
    public async Task<IActionResult> AbandonTie(Guid programId, Guid tieId, [FromBody] AbandonTieRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new AbandonTieCommand(programId, tieId, request.Reason), cancellationToken);
        return NoContent();
    }
}
