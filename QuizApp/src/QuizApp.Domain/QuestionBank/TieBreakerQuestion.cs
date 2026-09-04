using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

/// <summary>Purpose-written tie-break questions, including numeric-proximity
/// ("closest wins"). An organiser may instead point a TieBreakRule at any
/// other format (MCQ by default) — this format is for when a dedicated
/// question is wanted.</summary>
public sealed class TieBreakerQuestion : Question
{
    private TieBreakerQuestion()
    {
    }

    private TieBreakerQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.TieBreaker, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static TieBreakerQuestion CreateNumericProximity(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText, decimal numericAnswer,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new TieBreakerQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            TieBreakLevel = 1,
            AnswerMode = TieBreakAnswerMode.NumericProximity,
            NumericAnswer = numericAnswer,
        };
    }

    public static TieBreakerQuestion CreateExactText(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText, string answerText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            throw new ArgumentException("AnswerText is required in ExactText mode.", nameof(answerText));
        }

        return new TieBreakerQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            TieBreakLevel = 1,
            AnswerMode = TieBreakAnswerMode.ExactText,
            AnswerText = answerText,
        };
    }

    public static TieBreakerQuestion CreateWithOptions(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new TieBreakerQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            TieBreakLevel = 1,
            AnswerMode = TieBreakAnswerMode.Options,
        };
    }

    public int TieBreakLevel { get; private set; }
    public TieBreakAnswerMode AnswerMode { get; private set; }
    public string? AnswerText { get; private set; }
    public decimal? NumericAnswer { get; private set; }
}
