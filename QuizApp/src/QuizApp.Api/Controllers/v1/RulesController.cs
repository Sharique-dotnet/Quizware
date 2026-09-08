using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizApp.Api.Contracts.V1.Rules;
using QuizApp.Application.Authorization;
using QuizApp.Application.Rules.Commands;
using QuizApp.Application.Rules.Dtos;
using QuizApp.Application.Rules.Queries;

namespace QuizApp.Api.Controllers.v1;

[ApiController]
[Route("api/v1/programs/{programId:guid}/rules")]
[Authorize(Policy = Policies.CanManageProgram)]
public sealed class RulesController : ControllerBase
{
    private readonly ISender _sender;

    public RulesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> GetScoring(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new GetScoringRulesQuery(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPut("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> UpsertScoring(
        Guid programId, [FromBody] UpsertScoringRulesRequest request, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new UpsertScoringRulesCommand(programId, request.Rules.Select(ToAppDto).ToList()), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPost("scoring/reset-defaults")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> ResetScoringDefaults(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new ResetScoringDefaultsCommand(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpGet("selection")]
    public async Task<ActionResult<IReadOnlyList<SelectionRuleDto>>> GetSelection(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new GetSelectionRulesQuery(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPut("selection")]
    public async Task<ActionResult<IReadOnlyList<SelectionRuleDto>>> UpsertSelection(
        Guid programId, [FromBody] UpsertSelectionRulesRequest request, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new UpsertSelectionRulesCommand(programId, request.Rules.Select(ToAppDto).ToList()), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPost("selection/preview")]
    public async Task<ActionResult<SelectionPreviewResponse>> PreviewSelection(
        Guid programId, [FromBody] SelectionPreviewRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PreviewSelectionQuery(programId, request.StageId, request.FormatCode, request.QuestionCount), cancellationToken);
        return Ok(new SelectionPreviewResponse(
            result.PoolSize, result.EligibleAfterFilters, result.EligibleAfterRepeatPolicy,
            result.DifficultyMixRequested, result.DifficultyMixAchievable, result.CanSatisfy, result.Warnings));
    }

    [HttpGet("qualification")]
    public async Task<ActionResult<IReadOnlyList<QualificationRuleDto>>> GetQualification(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new GetQualificationRulesQuery(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPut("qualification")]
    public async Task<ActionResult<IReadOnlyList<QualificationRuleDto>>> UpsertQualification(
        Guid programId, [FromBody] UpsertQualificationRulesRequest request, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new UpsertQualificationRulesCommand(programId, request.Rules.Select(ToAppDto).ToList()), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpGet("tie-break")]
    public async Task<ActionResult<IReadOnlyList<TieBreakRuleDto>>> GetTieBreak(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new GetTieBreakRulesQuery(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPut("tie-break")]
    public async Task<ActionResult<IReadOnlyList<TieBreakRuleDto>>> UpsertTieBreak(
        Guid programId, [FromBody] UpsertTieBreakRulesRequest request, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new UpsertTieBreakRulesCommand(programId, request.Rules.Select(ToAppDto).ToList()), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    [HttpPost("tie-break/reset-defaults")]
    public async Task<ActionResult<IReadOnlyList<TieBreakRuleDto>>> ResetTieBreakDefaults(Guid programId, CancellationToken cancellationToken)
    {
        var rules = await _sender.Send(new ResetTieBreakDefaultsCommand(programId), cancellationToken);
        return Ok(rules.Select(ToResponse).ToList());
    }

    private static ScoringRuleDto ToResponse(ScoringRuleAppDto dto) => new(dto.Id, dto.FormatCode, dto.Outcome, dto.ContextKey, dto.Points);

    private static ScoringRuleAppDto ToAppDto(ScoringRuleDto dto) => new(dto.Id, dto.FormatCode, dto.Outcome, dto.ContextKey, dto.Points);

    private static SelectionRuleDto ToResponse(SelectionRuleAppDto dto) =>
        new(dto.Id, dto.FormatCode, dto.StageId, dto.DifficultyMixJson, dto.RepeatPolicy, dto.TopicSpreadPolicy, dto.FallbackPolicy);

    private static SelectionRuleAppDto ToAppDto(SelectionRuleDto dto) =>
        new(dto.Id, dto.FormatCode, dto.StageId, dto.DifficultyMixJson, dto.RepeatPolicy, dto.TopicSpreadPolicy, dto.FallbackPolicy);

    private static QualificationRuleDto ToResponse(QualificationRuleAppDto dto) =>
        new(dto.Id, dto.StageId, dto.WinnersPerMatch, dto.BestRemainingAcrossStage, dto.ManualWildcardSlots);

    private static QualificationRuleAppDto ToAppDto(QualificationRuleDto dto) =>
        new(dto.Id, dto.StageId, dto.WinnersPerMatch, dto.BestRemainingAcrossStage, dto.ManualWildcardSlots);

    private static TieBreakRuleDto ToResponse(TieBreakRuleAppDto dto) =>
        new(dto.Id, dto.StageId, dto.Criteria, dto.TieBreakFormat, dto.QuestionCount, dto.SuddenDeath, dto.MaxExtraRounds, dto.ScoreCountsTowardStage, dto.OnStillTied);

    private static TieBreakRuleAppDto ToAppDto(TieBreakRuleDto dto) =>
        new(dto.Id, dto.StageId, dto.Criteria, dto.TieBreakFormat, dto.QuestionCount, dto.SuddenDeath, dto.MaxExtraRounds, dto.ScoreCountsTowardStage, dto.OnStillTied);
}
