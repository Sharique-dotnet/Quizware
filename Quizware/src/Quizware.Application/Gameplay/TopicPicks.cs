using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Topic picks draw only from what the segment already reserved at
/// match start — a pick changes which reserved question is served next, never
/// what was reserved, so the match stays reproducible from its seed.</summary>
internal static class TopicPicks
{
    public sealed record Candidate(MatchQuestion MatchQuestion, Guid TopicId, string TopicName);

    public static async Task<(MatchSegment Segment, StageSegmentTemplate? Template)?> OpenSegmentAsync(
        IAppDbContext db, Guid matchId, CancellationToken cancellationToken)
    {
        var segment = await db.MatchSegments
            .SingleOrDefaultAsync(s => s.MatchId == matchId && s.State == MatchSegmentState.Open, cancellationToken);
        if (segment is null)
        {
            return null;
        }

        var template = await db.StageSegmentTemplates.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == segment.SegmentTemplateId, cancellationToken);
        return (segment, template);
    }

    public static async Task<List<Candidate>> CandidatesAsync(IAppDbContext db, Guid segmentId, CancellationToken cancellationToken)
    {
        var reserved = await LiveRules.ReservedInSegmentAsync(db, segmentId, cancellationToken);
        var questionIds = reserved.Select(q => q.QuestionId).ToList();
        var topicByQuestion = await db.Questions.IgnoreQueryFilters()
            .Where(q => questionIds.Contains(q.Id) && q.TopicId != null)
            .ToDictionaryAsync(q => q.Id, q => q.TopicId!.Value, cancellationToken);
        var topicIds = topicByQuestion.Values.Distinct().ToList();
        var topicNames = await db.Topics.IgnoreQueryFilters()
            .Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return reserved
            .Where(q => topicByQuestion.ContainsKey(q.QuestionId))
            .Select(q => new Candidate(q, topicByQuestion[q.QuestionId], topicNames[topicByQuestion[q.QuestionId]]))
            .ToList();
    }

    public static void RequireTopicPicks(StageSegmentTemplate? template)
    {
        if (template?.TopicSelectionMode != TopicSelectionMode.TeamPicksTopic)
        {
            throw new InvalidStateTransitionException("The open segment does not use team topic picks.");
        }
    }
}
