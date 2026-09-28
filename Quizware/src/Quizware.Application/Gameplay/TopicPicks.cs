using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Formats;
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
    /// <summary>A reserved question as it appears on the board.</summary>
    public sealed record Candidate(MatchQuestion MatchQuestion, Guid? TopicId, string TopicName, bool IsExclusive, int? DisplayOrder);

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

    /// <summary>Each still-reserved question under its board label: the
    /// format's own label when it has one (Choice's TopicLabel), otherwise the
    /// question's Topic name. Questions with neither are not on the board.</summary>
    public static async Task<List<Candidate>> CandidatesAsync(
        IAppDbContext db, QuestionFormatHandlers formats, Guid segmentId, CancellationToken cancellationToken)
    {
        var reserved = await LiveRules.ReservedInSegmentAsync(db, segmentId, cancellationToken);
        var questionIds = reserved.Select(q => q.QuestionId).ToList();
        var questions = await db.Questions.IgnoreQueryFilters()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);
        var topicIds = questions.Values.Where(q => q.TopicId != null).Select(q => q.TopicId!.Value).Distinct().ToList();
        var topicNames = await db.Topics.IgnoreQueryFilters()
            .Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var candidates = new List<Candidate>();
        foreach (var matchQuestion in reserved)
        {
            var question = questions[matchQuestion.QuestionId];
            var choice = formats.For(question.FormatCode).TopicChoice(question);
            var label = choice?.Label ?? (question.TopicId is { } topicId ? topicNames.GetValueOrDefault(topicId) : null);
            if (label is not null)
            {
                candidates.Add(new Candidate(matchQuestion, question.TopicId, label, choice?.IsExclusive ?? false, choice?.DisplayOrder));
            }
        }

        return candidates;
    }

    public static void RequireTopicPicks(StageSegmentTemplate? template)
    {
        if (template?.TopicSelectionMode != TopicSelectionMode.TeamPicksTopic)
        {
            throw new InvalidStateTransitionException("The open segment does not use team topic picks.");
        }
    }
}
