using Quizware.Domain.Gameplay;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Scoring;

public sealed record ScoredAnswer(ScoringRule Rule, int Points);

public sealed record ReversedScore(Guid ScoringRuleId, int PointsReversed);

/// <summary>P10-01/P10-02: the one place points change. Every call writes the
/// immutable ScoreEvent and moves TeamMatchScore — and TeamStageScore, when
/// the match counts toward its stage — by the same amount, inside the caller's
/// unit of work, so the read models can never drift from the ledger. None of
/// these methods call SaveChangesAsync.</summary>
public interface IScoringEngine
{
    /// <summary>Resolves the scoring rule (SCORING_RULE_MISSING if none) and
    /// scores <paramref name="answer"/>.</summary>
    Task<ScoredAnswer> ScoreAnswerAsync(
        Match match, MatchSegment segment, MatchParticipant participant, AnswerRecord answer, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>A pass is scored only when a rule for the Passed outcome
    /// exists; otherwise it is counted but carries no points.</summary>
    Task<int> ScorePassAsync(
        Match match, MatchSegment segment, MatchParticipant participant, AnswerRecord pass, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>P10-04: an opposite-signed ScoreEvent and the exact inverse
    /// read-model update, so totals return to their prior values.</summary>
    Task<ReversedScore> ReverseAnswerAsync(
        Match match, AnswerRecord original, Guid userId, string reason, CancellationToken cancellationToken);

    /// <summary>P10-03: a manual adjustment with its mandatory reason.</summary>
    Task<ScoreEvent> AdjustAsync(
        Match match, MatchParticipant participant, int points, string reason, Guid approvedByUserId,
        CancellationToken cancellationToken);

    /// <summary>Opens the match score rows, and the stage score rows the
    /// match's points will roll into.</summary>
    Task OpenScoresAsync(Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken);

    /// <summary>Counts the completed match in each team's stage record.</summary>
    Task RecordMatchCompletedAsync(Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken);

    /// <summary>An abandoned match has no result, so its points leave the stage totals.</summary>
    Task WithdrawAbandonedMatchAsync(Match match, CancellationToken cancellationToken);

    /// <summary>P10-06: rebuilds the match's TeamMatchScore rows from its
    /// ScoreEvent and AnswerRecord ledger.</summary>
    Task<IReadOnlyList<TeamMatchScore>> RecalculateMatchAsync(Match match, CancellationToken cancellationToken);

    /// <summary>P10-06: rebuilds a stage's TeamStageScore rows from the
    /// match totals of every counting, non-abandoned match in it.</summary>
    Task RecalculateStageAsync(Guid stageId, CancellationToken cancellationToken);
}
