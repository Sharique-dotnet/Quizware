using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Rules;
using QuizApp.Application.Authorization;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/rules")]
[Authorize(Policy = Policies.CanManageProgram)]
public sealed class RulesController : ControllerBase
{
    [HttpGet("scoring")]
    public ActionResult<IReadOnlyList<ScoringRuleDto>> GetScoring(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("scoring")]
    public ActionResult<IReadOnlyList<ScoringRuleDto>> UpsertScoring(Guid programId, [FromBody] UpsertScoringRulesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("scoring/reset-defaults")]
    public ActionResult<IReadOnlyList<ScoringRuleDto>> ResetScoringDefaults(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("selection")]
    public ActionResult<IReadOnlyList<SelectionRuleDto>> GetSelection(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("selection")]
    public ActionResult<IReadOnlyList<SelectionRuleDto>> UpsertSelection(Guid programId, [FromBody] UpsertSelectionRulesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("selection/preview")]
    public ActionResult<SelectionPreviewResponse> PreviewSelection(Guid programId, [FromBody] SelectionPreviewRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("qualification")]
    public ActionResult<IReadOnlyList<QualificationRuleDto>> GetQualification(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("qualification")]
    public ActionResult<IReadOnlyList<QualificationRuleDto>> UpsertQualification(Guid programId, [FromBody] UpsertQualificationRulesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("tie-break")]
    public ActionResult<IReadOnlyList<TieBreakRuleDto>> GetTieBreak(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("tie-break")]
    public ActionResult<IReadOnlyList<TieBreakRuleDto>> UpsertTieBreak(Guid programId, [FromBody] UpsertTieBreakRulesRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("tie-break/reset-defaults")]
    public ActionResult<IReadOnlyList<TieBreakRuleDto>> ResetTieBreakDefaults(Guid programId) => StatusCode(StatusCodes.Status501NotImplemented);
}
