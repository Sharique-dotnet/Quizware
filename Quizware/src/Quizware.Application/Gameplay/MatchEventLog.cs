using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

public static class MatchEventTypes
{
    public const string MatchStarted = "MatchStarted";
    public const string MatchPaused = "MatchPaused";
    public const string MatchResumed = "MatchResumed";
    public const string MatchCompleted = "MatchCompleted";
    public const string MatchAbandoned = "MatchAbandoned";
    public const string SegmentOpened = "SegmentOpened";
    public const string SegmentCompleted = "SegmentCompleted";
    public const string SegmentSkipped = "SegmentSkipped";
    public const string SegmentsReordered = "SegmentsReordered";
    public const string QuestionServed = "QuestionServed";
    public const string QuestionRevealed = "QuestionRevealed";
    public const string QuestionSkipped = "QuestionSkipped";
    public const string TopicSelected = "TopicSelected";
    public const string AnswerRecorded = "AnswerRecorded";
    public const string AnswerReversed = "AnswerReversed";
    public const string QuestionPassed = "QuestionPassed";
    public const string ParticipantDisqualified = "ParticipantDisqualified";
    public const string ParticipantReinstated = "ParticipantReinstated";
}

/// <summary>Appends to the append-only MatchEvent timeline. Scoped per
/// request: it remembers the last sequence number it handed out, so several
/// events added before one SaveChangesAsync still get distinct, increasing
/// numbers.</summary>
public sealed class MatchEventLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly Dictionary<Guid, long> _lastSequenceByMatch = new();

    public MatchEventLog(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task AppendAsync(Match match, string eventType, object payload, CancellationToken cancellationToken)
    {
        if (!_lastSequenceByMatch.TryGetValue(match.Id, out var last))
        {
            last = await _db.MatchEvents
                .Where(e => e.MatchId == match.Id)
                .Select(e => (long?)e.SequenceNumber)
                .MaxAsync(cancellationToken) ?? 0;
        }

        var next = last + 1;
        _lastSequenceByMatch[match.Id] = next;

        _db.MatchEvents.Add(MatchEvent.Create(
            match.ProgramId, match.Id, next, eventType, JsonSerializer.Serialize(payload, JsonOptions), _currentUser.UserId));
    }
}
