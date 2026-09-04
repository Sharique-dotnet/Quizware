using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

/// <summary>No options table — this format has no options at all.
/// <see cref="MediaAssetId"/> and <see cref="AnswerText"/> are non-nullable,
/// a real guarantee the shared base table could not express.</summary>
public sealed class AudioVisualQuestion : Question
{
    private AudioVisualQuestion()
    {
        AnswerText = string.Empty;
    }

    private AudioVisualQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId,
        Guid mediaAssetId, MediaKind mediaKind, string answerText)
        : base(programId, ownerScope, QuestionFormatCode.AudioVisual, questionText, difficultyLevel, language, createdBy, topicId)
    {
        MediaAssetId = mediaAssetId;
        MediaKind = mediaKind;
        AnswerText = answerText;
    }

    public static AudioVisualQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        Guid mediaAssetId, MediaKind mediaKind, string answerText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null)
    {
        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("MediaAssetId is required for an Audio/Visual question.", nameof(mediaAssetId));
        }

        if (string.IsNullOrWhiteSpace(answerText))
        {
            throw new ArgumentException("AnswerText is required for an Audio/Visual question.", nameof(answerText));
        }

        return new AudioVisualQuestion(
            programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId,
            mediaAssetId, mediaKind, answerText)
        {
            ReplayAllowed = true,
        };
    }

    public Guid MediaAssetId { get; private set; }
    public MediaKind MediaKind { get; private set; }
    public string AnswerText { get; private set; }
    public string? AcceptableAnswersJson { get; private set; }
    public int? PlaybackStartSeconds { get; private set; }
    public int? PlaybackDurationSeconds { get; private set; }
    public bool AutoPlay { get; private set; }
    public bool ReplayAllowed { get; private set; }
    public Guid? RevealMediaAssetId { get; private set; }
}
