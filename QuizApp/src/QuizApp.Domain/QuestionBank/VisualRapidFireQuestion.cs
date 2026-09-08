using QuizApp.Domain.Enums;

namespace QuizApp.Domain.QuestionBank;

/// <summary>Served as a set of images (see <see cref="VisualRapidFireItem"/>),
/// which a single shared question table could not express cleanly.</summary>
public sealed class VisualRapidFireQuestion : Question
{
    private VisualRapidFireQuestion()
    {
    }

    private VisualRapidFireQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId, int imageCount)
        : base(programId, ownerScope, QuestionFormatCode.VisualRapidFire, questionText, difficultyLevel, language, createdBy, topicId)
    {
        ImageCount = imageCount;
    }

    public static VisualRapidFireQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, int imageCount,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        int? revealSecondsPerImage = null, int? gridColumns = null, bool scorePerImage = true)
    {
        if (imageCount < 1)
        {
            throw new ArgumentException("A Visual Rapid Fire question needs at least one image.", nameof(imageCount));
        }

        return new VisualRapidFireQuestion(programId, ownerScope, questionText: null, difficultyLevel, language, createdBy, topicId, imageCount)
        {
            RevealSecondsPerImage = revealSecondsPerImage,
            GridColumns = gridColumns,
            ScorePerImage = scorePerImage,
        };
    }

    public int ImageCount { get; private set; }
    public int? RevealSecondsPerImage { get; private set; }
    public int? GridColumns { get; private set; }
    public bool ScorePerImage { get; private set; }
}
