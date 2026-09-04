using QuizApp.Domain.Common;

namespace QuizApp.Domain.Gameplay;

/// <summary>The replayable timeline. Append-only — lets you replay a whole
/// match, prove what happened in a dispute, and rebuild the read model.</summary>
public sealed class MatchEvent : BaseEntity
{
    private MatchEvent()
    {
        EventType = string.Empty;
        PayloadJson = string.Empty;
    }

    public static MatchEvent Create(
        Guid programId, Guid matchId, long sequenceNumber, string eventType, string payloadJson, Guid? actorUserId = null)
    {
        if (sequenceNumber < 1)
        {
            throw new ArgumentException("SequenceNumber is 1-based and strictly increasing per match.", nameof(sequenceNumber));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("EventType is required.", nameof(eventType));
        }

        return new MatchEvent
        {
            ProgramId = programId,
            MatchId = matchId,
            SequenceNumber = sequenceNumber,
            EventType = eventType,
            PayloadJson = payloadJson,
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = actorUserId,
        };
    }

    /// <summary>The next sequence number for a match given its events so far
    /// (0 if none exist yet).</summary>
    public static long NextSequenceNumber(IReadOnlyList<MatchEvent> existingEventsForMatch) =>
        existingEventsForMatch.Count == 0 ? 1 : existingEventsForMatch.Max(e => e.SequenceNumber) + 1;

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public long SequenceNumber { get; private set; }
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }
}
