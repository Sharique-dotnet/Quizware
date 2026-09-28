using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Scores;
using Quizware.Application.Authorization;
using Quizware.Application.Scoring.Commands;
using Quizware.Application.Scoring.Dtos;
using Quizware.Application.Scoring.Queries;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/matches/{matchId:guid}/scores")]
[Authorize]
public sealed class ScoresController : ControllerBase
{
    private readonly ISender _sender;

    public ScoresController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewLive)]
    public async Task<ActionResult<MatchScoresResponse>> GetScores(Guid matchId, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(new GetMatchScoresQuery(matchId), cancellationToken)));

    [HttpGet("events")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Operator},{Roles.Auditor}")]
    public async Task<ActionResult<ScoreEventsResponse>> GetEvents(Guid matchId, CancellationToken cancellationToken)
    {
        var events = await _sender.Send(new GetScoreEventsQuery(matchId), cancellationToken);
        return Ok(new ScoreEventsResponse(
            events.Select(e => new ScoreEventDto(e.Id, e.TeamId, e.Points, e.Reason, e.IsReversed, e.OccurredAtUtc)).ToList()));
    }

    [HttpPost("adjust")]
    [Authorize(Policy = Policies.CanAdjustScore)]
    public async Task<ActionResult<MatchScoresResponse>> Adjust(
        Guid matchId, [FromBody] AdjustScoreRequest request, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(
            new AdjustScoreCommand(matchId, request.TeamId, request.PointsDelta, request.Reason), cancellationToken)));

    [HttpPost("recalculate")]
    [Authorize(Policy = Policies.CanAdjustScore)]
    public async Task<ActionResult<RecalculateScoresResponse>> Recalculate(Guid matchId, CancellationToken cancellationToken)
    {
        var scores = await _sender.Send(new RecalculateScoresCommand(matchId), cancellationToken);
        return Ok(new RecalculateScoresResponse(ToDtos(scores)));
    }

    private static MatchScoresResponse ToResponse(IReadOnlyList<TeamScoreItemDto> scores) => new(ToDtos(scores));

    private static IReadOnlyList<TeamScoreDto> ToDtos(IReadOnlyList<TeamScoreItemDto> scores) =>
        scores.Select(s => new TeamScoreDto(s.TeamId, s.TeamName, s.Score, s.Rank)).ToList();
}
