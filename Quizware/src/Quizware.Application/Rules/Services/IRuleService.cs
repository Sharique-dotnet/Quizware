using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Rules.Services;

/// <summary>P7-10: resolves the most specific <see cref="ScoringRule"/> for
/// a given outcome — segment rule beats stage rule beats program rule.
/// Never guesses: throws <see cref="Quizware.Domain.Common.Exceptions.ScoringRuleNotFoundException"/>
/// (mapped to SCORING_RULE_MISSING) rather than falling back to some
/// arbitrary default when nothing matches.</summary>
public interface IRuleService
{
    Task<ScoringRule> ResolveScoringRuleAsync(
        Guid programId, QuestionFormatCode formatCode, AnswerOutcome outcome,
        Guid? stageId, Guid? segmentTemplateId, string? contextKey, CancellationToken cancellationToken);

    /// <summary>Same specificity-based resolution as scoring, but a
    /// selection rule is optional — the caller falls back to
    /// <see cref="QuestionSelectionRule.Create"/>'s own defaults (every
    /// difficulty, NeverInProgram, no topic spread, WidenThenFail) when
    /// nothing has been configured, rather than treating an absent rule
    /// as an error.</summary>
    Task<QuestionSelectionRule?> ResolveSelectionRuleAsync(
        Guid programId, QuestionFormatCode formatCode, Guid? stageId, Guid? segmentTemplateId,
        CancellationToken cancellationToken);
}
