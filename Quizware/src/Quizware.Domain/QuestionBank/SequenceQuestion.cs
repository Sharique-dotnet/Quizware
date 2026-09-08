using Quizware.Domain.Enums;

namespace Quizware.Domain.QuestionBank;

public sealed class SequenceQuestion : Question
{
    private SequenceQuestion()
    {
    }

    private SequenceQuestion(
        Guid? programId, QuestionOwnerScope ownerScope, string? questionText,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId, int sequenceLength)
        : base(programId, ownerScope, QuestionFormatCode.Sequence, questionText, difficultyLevel, language, createdBy, topicId)
    {
        SequenceLength = sequenceLength;
    }

    public static SequenceQuestion Create(
        Guid? programId, QuestionOwnerScope ownerScope, string questionText, int sequenceLength,
        DifficultyLevel difficultyLevel, string language, string createdBy, Guid? topicId = null,
        bool partialCreditEnabled = false, int? pointsPerCorrectPosition = null, SequenceItemKind itemKind = SequenceItemKind.Text)
    {
        if (sequenceLength < 2)
        {
            throw new ArgumentException("A sequence needs at least 2 items to order.", nameof(sequenceLength));
        }

        return new SequenceQuestion(programId, ownerScope, questionText, difficultyLevel, language, createdBy, topicId, sequenceLength)
        {
            ItemKind = itemKind,
            PartialCreditEnabled = partialCreditEnabled,
            PointsPerCorrectPosition = pointsPerCorrectPosition,
        };
    }

    public int SequenceLength { get; private set; }
    public bool PartialCreditEnabled { get; private set; }
    public int? PointsPerCorrectPosition { get; private set; }
    public SequenceItemKind ItemKind { get; private set; }
}
