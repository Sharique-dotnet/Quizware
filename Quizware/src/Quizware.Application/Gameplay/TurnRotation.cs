using Quizware.Application.Gameplay.Formats;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Whose turn it is. BR-2.2: question N of a segment goes to active
/// participant N mod count, so each segment starts again with the first team
/// in turn order. BR-2.4: in formats any team may answer, nobody holds the
/// question.</summary>
internal static class TurnRotation
{
    public static Guid? NextParticipantOrNull(
        IReadOnlyList<MatchParticipant> participants, MatchSegment segment, QuestionFormatHandlers formats)
    {
        if (formats.For(segment.FormatCode).AnyTeamMayAnswer || participants.All(p => p.Status != ParticipantStatus.Active))
        {
            return null;
        }

        return TurnOrderCalculator.GetNextParticipant(participants, segment.ServedQuestionCount);
    }
}
