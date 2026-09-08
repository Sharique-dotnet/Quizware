using QuizApp.Domain.Enums;
using QuizApp.Domain.Scoring;

namespace QuizApp.Application.Rules.Services;

/// <summary>P7-10: resolves the most specific <see cref="ScoringRule"/> for
/// a given outcome — segment rule beats stage rule beats program rule.
/// Never guesses: throws <see cref="QuizApp.Domain.Common.Exceptions.ScoringRuleNotFoundException"/>
/// (mapped to SCORING_RULE_MISSING) rather than falling back to some
/// arbitrary default when nothing matches.</summary>
public interface IRuleService
{
    Task<ScoringRule> ResolveScoringRuleAsync(
        Guid programId, QuestionFormatCode formatCode, AnswerOutcome outcome,
        Guid? stageId, Guid? segmentTemplateId, string? contextKey, CancellationToken cancellationToken);
}
