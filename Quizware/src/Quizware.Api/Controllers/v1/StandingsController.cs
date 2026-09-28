using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Standings;
using Quizware.Application.Scoring.Dtos;
using Quizware.Application.Scoring.Queries;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/standings")]
[Authorize]
public sealed class StandingsController : ControllerBase
{
    private readonly ISender _sender;

    public StandingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("overall")]
    public async Task<ActionResult<OverallStandingsResponse>> Overall(Guid programId, CancellationToken cancellationToken)
    {
        var standings = await _sender.Send(new GetOverallStandingsQuery(programId), cancellationToken);
        return Ok(new OverallStandingsResponse(ToEntries(standings)));
    }

    [HttpGet("stages/{stageId:guid}")]
    public async Task<ActionResult<StageStandingsResponse>> Stage(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(ToResponse(await _sender.Send(new GetStageStandingsQuery(programId, stageId), cancellationToken)));

    [HttpGet("teams/{teamId:guid}")]
    public async Task<ActionResult<TeamStandingResponse>> Team(Guid programId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await _sender.Send(new GetTeamStandingQuery(programId, teamId), cancellationToken);
        return Ok(new TeamStandingResponse(
            team.TeamId,
            team.Matches.Select(m => new TeamStandingRecord(m.MatchId, m.MatchName, m.Score, m.Rank)).ToList(),
            team.OverallScore,
            team.OverallRank));
    }

    internal static StageStandingsResponse ToResponse(StageStandingsDto dto) =>
        new(dto.StageId, ToEntries(dto.Standings), dto.TieBreakNotes);

    private static IReadOnlyList<StandingEntry> ToEntries(IReadOnlyList<StandingEntryDto> standings) =>
        standings.Select(s => new StandingEntry(s.TeamId, s.TeamName, s.Score, s.Rank)).ToList();
}
