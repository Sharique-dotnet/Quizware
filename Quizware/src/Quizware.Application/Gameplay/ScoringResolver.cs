using Quizware.Application.Rules.Services;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Scoring rules may carry a ContextKey — "Direct" for an answer a
/// team got first-hand, "AfterPass" for one it took over after a pass. The
/// context-specific rule wins; the generic (no context) rule is used only when
/// no context-specific one exists. Nothing is ever invented: with neither,
/// SCORING_RULE_MISSING.</summary>
public sealed class ScoringResolver
{
    public const string DirectContext = "Direct";
    public const string AfterPassContext = "AfterPass";

    private readonly IRuleService _ruleService;

    public ScoringResolver(IRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public async Task<ScoringRule> ResolveAsync(
        Match match, MatchSegment segment, AnswerOutcome outcome, int passNumber, CancellationToken cancellationToken)
    {
        var context = passNumber > 0 ? AfterPassContext : DirectContext;
        try
        {
            return await _ruleService.ResolveScoringRuleAsync(
                match.ProgramId, segment.FormatCode, outcome, match.StageId, segment.SegmentTemplateId, context, cancellationToken);
        }
        catch (ScoringRuleNotFoundException)
        {
            return await _ruleService.ResolveScoringRuleAsync(
                match.ProgramId, segment.FormatCode, outcome, match.StageId, segment.SegmentTemplateId, null, cancellationToken);
        }
    }

    public async Task<ScoringRule?> TryResolveAsync(
        Match match, MatchSegment segment, AnswerOutcome outcome, int passNumber, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveAsync(match, segment, outcome, passNumber, cancellationToken);
        }
        catch (ScoringRuleNotFoundException)
        {
            return null;
        }
    }
}
