using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Reports;
using Quizware.Application.Abstractions;
using Quizware.Application.Authorization;
using Quizware.Application.Reporting;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/reports")]
[Authorize(Policy = Policies.CanViewReports)]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("matches/{matchId:guid}")]
    public async Task<ActionResult<MatchReportResponse>> MatchReport(Guid programId, Guid matchId, CancellationToken cancellationToken) =>
        Ok(ReportMapper.ToResponse(await _sender.Send(new GetMatchReportQuery(programId, matchId), cancellationToken)));

    [HttpGet("stages/{stageId:guid}")]
    public async Task<ActionResult<StageSummaryReportResponse>> StageSummary(Guid programId, Guid stageId, CancellationToken cancellationToken) =>
        Ok(ReportMapper.ToResponse(await _sender.Send(new GetStageSummaryReportQuery(programId, stageId), cancellationToken)));

    [HttpGet("teams/{teamId:guid}")]
    public async Task<ActionResult<TeamPerformanceReportResponse>> TeamPerformance(Guid programId, Guid teamId, CancellationToken cancellationToken) =>
        Ok(ReportMapper.ToResponse(await _sender.Send(new GetTeamPerformanceReportQuery(programId, teamId), cancellationToken)));

    /// <summary>Optionally narrowed to one stage; <paramref name="flaggedOnly"/>
    /// returns only questions carrying a quality flag.</summary>
    [HttpGet("questions/usage")]
    public async Task<ActionResult<QuestionUsageReportResponse>> QuestionUsage(
        Guid programId, [FromQuery] Guid? stageId, [FromQuery] bool flaggedOnly, CancellationToken cancellationToken) =>
        Ok(ReportMapper.ToResponse(await _sender.Send(new GetQuestionUsageReportQuery(programId, stageId, flaggedOnly), cancellationToken)));

    /// <summary>Newest first. <paramref name="entityName"/> is the entity type
    /// (e.g. "Team"); <paramref name="actor"/> is a user id or part of a user
    /// name; <paramref name="from"/>/<paramref name="to"/> are inclusive UTC bounds.</summary>
    [HttpGet("audit")]
    public async Task<ActionResult<AuditReportResponse>> Audit(
        Guid programId,
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] string? actor,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100) =>
        Ok(ReportMapper.ToResponse(await _sender.Send(
            new GetAuditLogQuery(new AuditLogFilter(programId, entityName, entityId, action, actor, ToUtc(from), ToUtc(to), page, pageSize)),
            cancellationToken)));

    [HttpGet("export/{reportType}")]
    public IActionResult Export(Guid programId, string reportType, [FromQuery] string format = "xlsx") => StatusCode(StatusCodes.Status501NotImplemented);

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        _ => value,
    };
}
