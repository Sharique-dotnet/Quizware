using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Reports;
using Quizware.Application.Authorization;
using Quizware.Application.Reporting;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/reports")]
[Authorize(Policy = Policies.CanManageProgram)]
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
    public ActionResult<TeamPerformanceReportResponse> TeamPerformance(Guid programId, Guid teamId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("questions/usage")]
    public ActionResult<QuestionUsageReportResponse> QuestionUsage(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("audit")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ProgramAdmin},{Roles.Auditor}")]
    public ActionResult<AuditReportResponse> Audit(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("export/{reportType}")]
    public IActionResult Export(Guid programId, string reportType, [FromQuery] string format = "xlsx") => StatusCode(StatusCodes.Status501NotImplemented);
}
