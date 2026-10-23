using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Display;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Application.Authorization;
using Quizware.Application.Display;

namespace Quizware.Api.Controllers.v1;

/// <summary>P12-07: read-only screen payloads for a display token.</summary>
[ApiController]
[Route("api/v1/display")]
[Authorize(Policy = Policies.DisplayOnly)]
public sealed class DisplayController : ControllerBase
{
    private readonly ISender _sender;

    public DisplayController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("matches/{matchId:guid}/state")]
    public async Task<ActionResult<DisplayMatchStateResponse>> MatchState(Guid matchId, CancellationToken cancellationToken)
    {
        var display = await _sender.Send(new GetDisplayMatchStateQuery(matchId), cancellationToken);
        var live = LiveMapper.ToResponse(display.Live);
        return Ok(new DisplayMatchStateResponse(
            live.MatchId,
            live.State,
            live.CurrentSegment?.Format,
            live.CurrentQuestion?.QuestionText,
            display.AnswerRevealed,
            live.MatchName,
            live.StageName,
            live.IsPaused,
            live.CurrentQuestion,
            live.ActiveParticipant,
            live.Participants,
            live.Progress));
    }

    [HttpGet("matches/{matchId:guid}/scores")]
    public async Task<ActionResult<DisplayScoresResponse>> MatchScores(Guid matchId, CancellationToken cancellationToken) =>
        Ok(new DisplayScoresResponse((await _sender.Send(new GetDisplayScoresQuery(matchId), cancellationToken))
            .Select(s => new DisplayScoreEntry(s.TeamId, s.TeamName, s.Score))
            .ToList()));

    [HttpGet("programs/{programId:guid}/standings")]
    public async Task<ActionResult<DisplayStandingsResponse>> Standings(Guid programId, CancellationToken cancellationToken) =>
        Ok(new DisplayStandingsResponse((await _sender.Send(new GetDisplayStandingsQuery(programId), cancellationToken))
            .Select(s => new DisplayStandingEntry(s.TeamId, s.TeamName, s.Score, s.Rank))
            .ToList()));

    [HttpGet("programs/{programId:guid}/branding")]
    public async Task<ActionResult<DisplayBrandingResponse>> Branding(Guid programId, CancellationToken cancellationToken)
    {
        var branding = await _sender.Send(new GetDisplayBrandingQuery(programId), cancellationToken);
        return Ok(new DisplayBrandingResponse(branding.LogoUrl, branding.PrimaryColor, branding.SecondaryColor, branding.FontFamily));
    }
}
