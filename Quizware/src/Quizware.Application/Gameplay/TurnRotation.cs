using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Whose turn it is. The rotation runs across the whole match —
/// question N of the match goes to active participant N mod count — so it
/// carries over between segments instead of restarting with the same team.</summary>
internal static class TurnRotation
{
    public static Guid? NextParticipantOrNull(IReadOnlyList<MatchParticipant> participants, IReadOnlyList<MatchSegment> segments)
    {
        if (participants.All(p => p.Status != ParticipantStatus.Active))
        {
            return null;
        }

        return TurnOrderCalculator.GetNextParticipant(participants, segments.Sum(s => s.ServedQuestionCount));
    }
}
