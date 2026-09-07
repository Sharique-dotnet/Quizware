using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Scoring;

public sealed record DefaultScoringValue(QuestionFormatCode FormatCode, AnswerOutcome Outcome, string? ContextKey, int Points);

/// <summary>
/// The legacy Contants.cs values (01-Analysis-Findings.md §1.4d), kept as
/// data here so Phase 6's "create a program" flow can seed a ScoringRule
/// per format/outcome for every new program — ScoringRule.ProgramId is
/// required, so these cannot be seeded as global rows ahead of any program
/// existing (P4-20 originally described them as a migration seed; they are
/// seeded per-program instead, consistent with the rest of the design).
/// </summary>
public static class DefaultScoringValues
{
    public static readonly IReadOnlyList<DefaultScoringValue> All =
    [
        new(QuestionFormatCode.Mcq, AnswerOutcome.Correct, null, 10),
        new(QuestionFormatCode.Mcq, AnswerOutcome.Incorrect, null, 0),
        new(QuestionFormatCode.AudioVisual, AnswerOutcome.Correct, null, 10),
        new(QuestionFormatCode.Sequence, AnswerOutcome.Correct, null, 20),
        new(QuestionFormatCode.Buzzer, AnswerOutcome.Correct, null, 20),
        new(QuestionFormatCode.Buzzer, AnswerOutcome.Incorrect, null, -15),
        new(QuestionFormatCode.Buzzer, AnswerOutcome.NoAnswer, null, -15),
        new(QuestionFormatCode.RapidFire, AnswerOutcome.Correct, null, 5),
        new(QuestionFormatCode.RapidFire, AnswerOutcome.Incorrect, null, -5),
        new(QuestionFormatCode.Passing, AnswerOutcome.Correct, "Direct", 15),
        new(QuestionFormatCode.Passing, AnswerOutcome.PassedCorrect, "AfterPass", 10),
        new(QuestionFormatCode.Passing, AnswerOutcome.Incorrect, null, -10),
        new(QuestionFormatCode.Card, AnswerOutcome.Correct, null, 5),
        new(QuestionFormatCode.Card, AnswerOutcome.Incorrect, null, -5),
        new(QuestionFormatCode.VisualRapidFire, AnswerOutcome.Correct, null, 5),
        new(QuestionFormatCode.VisualRapidFire, AnswerOutcome.Incorrect, null, -5),
        new(QuestionFormatCode.Choice, AnswerOutcome.Correct, null, 15),
        new(QuestionFormatCode.Choice, AnswerOutcome.Incorrect, null, -15),
    ];
}
