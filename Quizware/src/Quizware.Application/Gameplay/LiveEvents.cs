namespace Quizware.Application.Gameplay;

/// <summary>The server→client events of the match and display hubs
/// (05-API-Design.md §5.7). Every one is written to the outbox in the same
/// transaction as the change it announces, then pushed by the dispatcher.</summary>
public static class LiveEvents
{
    public const string MatchStateChanged = "MatchStateChanged";
    public const string QuestionServed = "QuestionServed";
    public const string QuestionRevealed = "QuestionRevealed";
    public const string QuestionSkipped = "QuestionSkipped";
    public const string QuestionPassed = "QuestionPassed";
    public const string TopicSelected = "TopicSelected";
    public const string AnswerRecorded = "AnswerRecorded";
    public const string AnswerReversed = "AnswerReversed";
    public const string ScoresUpdated = "ScoresUpdated";
    public const string ParticipantRemoved = "ParticipantRemoved";
    public const string ParticipantReinstated = "ParticipantReinstated";
    public const string SegmentChanged = "SegmentChanged";
    public const string SegmentOrderChanged = "SegmentOrderChanged";
    public const string TieBreakStarted = "TieBreakStarted";
    public const string TieBreakResolved = "TieBreakResolved";
    public const string TimerTick = "TimerTick";
    public const string MatchCompleted = "MatchCompleted";

    /// <summary>The live event a match-timeline entry announces, if any.
    /// Start, answers, disqualification, completion and tie-break results
    /// are published by their own handlers with a richer payload.</summary>
    public static string? ForTimelineEvent(string eventType) => eventType switch
    {
        MatchEventTypes.MatchPaused or MatchEventTypes.MatchResumed or MatchEventTypes.MatchAbandoned => MatchStateChanged,
        MatchEventTypes.SegmentOpened or MatchEventTypes.SegmentCompleted or MatchEventTypes.SegmentSkipped => SegmentChanged,
        MatchEventTypes.SegmentsReordered => SegmentOrderChanged,
        MatchEventTypes.QuestionServed => QuestionServed,
        MatchEventTypes.QuestionRevealed => QuestionRevealed,
        MatchEventTypes.QuestionSkipped => QuestionSkipped,
        MatchEventTypes.QuestionPassed => QuestionPassed,
        MatchEventTypes.TopicSelected => TopicSelected,
        MatchEventTypes.AnswerReversed => AnswerReversed,
        MatchEventTypes.ParticipantReinstated => ParticipantReinstated,
        MatchEventTypes.ScoreAdjusted or MatchEventTypes.ScoresRecalculated => ScoresUpdated,
        _ => null,
    };
}
