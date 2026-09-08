using Quizware.Domain.Enums;

namespace Quizware.Domain.QuestionBank;

/// <summary>No options table. <see cref="IsHostRead"/> supports both "read off
/// paper, only the verdict is stored" and stored-question modes without a
/// schema change.</summary>
public sealed class RapidFireQuestion : Question
{
    private RapidFireQuestion()
    {
    }

    private RapidFireQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId)
        : base(programId, ownerScope, QuestionFormatCode.RapidFire, questionText, difficultyLevel, language, createdBy, topicId)
    {
    }

    public static RapidFireQuestion CreateStored(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText, string answerText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            throw new ArgumentException("AnswerText is required when the question is stored, not host-read.", nameof(answerText));
        }

        return new RapidFireQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId)
        {
            AnswerText = answerText,
            IsHostRead = false,
        };
    }

    public static RapidFireQuestion CreateHostRead(
        Guid? programId, QuestionOwnerScope ownerScope,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        return new RapidFireQuestion(programId, ownerScope, questionText: null, difficultyLevel, language, createdBy, topicId)
        {
            IsHostRead = true,
        };
    }

    public string? AnswerText { get; private set; }
    public string? AcceptableAnswersJson { get; private set; }
    public bool IsHostRead { get; private set; }
    public int? BurstSeconds { get; private set; }
}
