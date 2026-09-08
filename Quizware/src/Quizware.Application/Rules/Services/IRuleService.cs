using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;

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
}
