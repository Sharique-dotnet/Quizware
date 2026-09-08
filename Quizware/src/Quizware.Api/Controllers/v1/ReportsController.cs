using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quizware.Api.Contracts.V1.Reports;
using Quizware.Application.Authorization;

namespace Quizware.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/reports")]
[Authorize(Policy = Policies.CanManageProgram)]
public sealed class ReportsController : ControllerBase
{
    [HttpGet("matches/{matchId:guid}")]
    public ActionResult<MatchReportResponse> MatchReport(Guid programId, Guid matchId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("stages/{stageId:guid}")]
    public ActionResult<StageSummaryReportResponse> StageSummary(Guid programId, Guid stageId) => StatusCode(StatusCodes.Status501NotImplemented);

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
