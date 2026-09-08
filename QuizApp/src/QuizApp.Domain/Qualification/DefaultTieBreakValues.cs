using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Qualification;

public sealed record DefaultTieBreakValue(
    string Name,
    TieBreakScope Scope,
    IReadOnlyList<string> Criteria,
    QuestionFormatCode TieBreakFormatCode,
    int QuestionCount,
    bool SuddenDeath,
    int MaxExtraRounds,
    bool ScoreCountsTowardStage,
    OnStillTiedPolicy OnStillTied);

/// <summary>
/// The three recommended seed rows from 04-Database-Schema.md §TieBreakRule
/// (League/StageQualification/MCQ/3 questions/no sudden death; Semi-Final/
/// MatchRanking/MCQ/3/no; Final/FinalPlacement/MCQ/5/yes), kept as data here
/// so P7-09's "reset-to-defaults" action and the P7-12 seed script share one
/// source, mirroring <see cref="Scoring.DefaultScoringValues"/>'s pattern.
/// </summary>
public static class DefaultTieBreakValues
{
    public static readonly IReadOnlyList<DefaultTieBreakValue> All =
    [
        new("League tie-break", TieBreakScope.StageQualification, ["HeadToHead", "HigherDifficultyCorrect"],
            QuestionFormatCode.Mcq, 3, SuddenDeath: false, MaxExtraRounds: 3, ScoreCountsTowardStage: false,
            OnStillTiedPolicy.ManualDecision),
        new("Semi-final tie-break", TieBreakScope.MatchRanking, ["HeadToHead", "HigherDifficultyCorrect"],
            QuestionFormatCode.Mcq, 3, SuddenDeath: false, MaxExtraRounds: 3, ScoreCountsTowardStage: false,
            OnStillTiedPolicy.ManualDecision),
        new("Final tie-break", TieBreakScope.FinalPlacement, ["HeadToHead", "HigherDifficultyCorrect"],
            QuestionFormatCode.Mcq, 5, SuddenDeath: true, MaxExtraRounds: 3, ScoreCountsTowardStage: false,
            OnStillTiedPolicy.ManualDecision),
    ];
}
