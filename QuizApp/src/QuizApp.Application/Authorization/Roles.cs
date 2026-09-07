namespace QuizApp.Application.Authorization;

/// <summary>Exactly 7 roles (ADR-009) — no Judge role. ProgramAdmin and
/// SuperAdmin hold sole authority over disqualification, answer reversal,
/// score adjustment, and manual tie-break resolution.</summary>
public static class Roles
{
    public const string SuperAdmin = nameof(SuperAdmin);
    public const string ProgramAdmin = nameof(ProgramAdmin);
    public const string QuestionAuthor = nameof(QuestionAuthor);
    public const string Operator = nameof(Operator);
    public const string Scorer = nameof(Scorer);
    public const string Display = nameof(Display);
    public const string Auditor = nameof(Auditor);

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, ProgramAdmin, QuestionAuthor, Operator, Scorer, Display, Auditor,
    ];
}
