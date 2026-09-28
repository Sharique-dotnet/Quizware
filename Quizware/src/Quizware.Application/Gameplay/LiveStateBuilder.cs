using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Authorization;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Builds the live console/display view of a match — the response
/// every live action returns, so the caller never has to re-fetch state.</summary>
public sealed class LiveStateBuilder
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public LiveStateBuilder(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>Operators may see the answer before it is revealed; a display
    /// screen (or anyone else with only view access) may not.</summary>
    public bool CallerMaySeeAnswers =>
        _currentUser.IsInRole(Roles.SuperAdmin) || _currentUser.IsInRole(Roles.ProgramAdmin)
        || _currentUser.IsInRole(Roles.Operator) || _currentUser.IsInRole(Roles.Scorer);

    public async Task<LiveMatchStateDto> BuildAsync(Match match, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var teamNames = await MatchSetup.TeamNamesAsync(_db, participants.Select(p => p.TeamId), cancellationToken);
        var scores = await _db.TeamMatchScores
            .Where(s => s.MatchId == match.Id)
            .ToDictionaryAsync(s => s.MatchParticipantId, s => s.TotalPoints, cancellationToken);

        var openSegment = segments.SingleOrDefault(s => s.State == MatchSegmentState.Open);
        var activeQuestion = await _db.MatchQuestions
            .SingleOrDefaultAsync(q => q.MatchId == match.Id && q.State == MatchQuestionState.Active, cancellationToken);

        CurrentSegmentDto? currentSegment = null;
        if (openSegment is not null)
        {
            var template = await _db.StageSegmentTemplates.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == openSegment.SegmentTemplateId, cancellationToken);
            currentSegment = new CurrentSegmentDto(
                openSegment.Id,
                openSegment.FormatCode.ToString(),
                template?.DisplayName ?? openSegment.FormatCode.ToString(),
                openSegment.OrderIndex,
                openSegment.State.ToString(),
                openSegment.PlannedQuestionCount,
                openSegment.ServedQuestionCount,
                (template?.TopicSelectionMode ?? TopicSelectionMode.None).ToString());
        }

        var currentQuestion = activeQuestion is null ? null : await PresentAsync(activeQuestion, cancellationToken);

        ActiveParticipantDto? activeParticipant = null;
        var activeParticipantId = activeQuestion?.TargetParticipantId
            ?? (openSegment is null || match.State != MatchState.InProgress
                ? null
                : TurnRotation.NextParticipantOrNull(participants, segments));
        if (activeParticipantId is not null)
        {
            var p = participants.Single(x => x.Id == activeParticipantId);
            activeParticipant = new ActiveParticipantDto(p.Id, p.TeamId, teamNames.GetValueOrDefault(p.TeamId, string.Empty), p.SeatNumber, p.TurnOrder);
        }

        var lastAnswer = await _db.AnswerRecords
            .Where(a => a.MatchId == match.Id && !a.IsReversed && a.Outcome != AnswerOutcome.Voided)
            .OrderByDescending(a => a.AnsweredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var countedSegments = segments.Where(s => s.State != MatchSegmentState.Skipped).ToList();

        return new LiveMatchStateDto(
            match.Id,
            MatchSetup.DisplayName(match),
            stage.Name,
            match.State.ToString(),
            match.State == MatchState.Paused,
            currentSegment,
            currentQuestion,
            activeParticipant,
            participants
                .Select(p => new LiveParticipantScoreDto(
                    p.Id, teamNames.GetValueOrDefault(p.TeamId, string.Empty), p.SeatNumber, p.TurnOrder,
                    p.Status.ToString(), scores.GetValueOrDefault(p.Id)))
                .ToList(),
            new MatchProgressDto(
                segments.Sum(s => s.ServedQuestionCount),
                countedSegments.Sum(s => s.PlannedQuestionCount),
                segments.Count(s => s.State is MatchSegmentState.Completed or MatchSegmentState.Skipped),
                segments.Count),
            BuzzerStatus,
            lastAnswer is not null && match.State is MatchState.InProgress or MatchState.Paused,
            lastAnswer?.Id);
    }

    /// <summary>No buzzer provider is wired into the engine yet (Phase 14),
    /// so the honest answer is always "unavailable".</summary>
    public static LiveBuzzerStatusDto BuzzerStatus => new(false, null, "Unavailable");

    public async Task<CurrentQuestionDto> PresentAsync(MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        var question = await _db.Questions.IgnoreQueryFilters()
            .SingleAsync(q => q.Id == matchQuestion.QuestionId, cancellationToken);
        var options = await _db.QuestionOptions
            .Where(o => o.QuestionId == question.Id)
            .ToListAsync(cancellationToken);

        string? topicName = null;
        var topicId = matchQuestion.SelectedTopicId ?? question.TopicId;
        if (topicId is not null)
        {
            topicName = await _db.Topics.IgnoreQueryFilters()
                .Where(t => t.Id == topicId)
                .Select(t => t.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var orderedOptions = OrderOptions(options, matchQuestion.OptionOrderJson)
            .Select((o, i) => new CurrentQuestionOptionDto(o.Id, o.OptionText, i))
            .ToList();

        var mayRevealAnswer = CallerMaySeeAnswers || matchQuestion.RevealedAtUtc is not null;
        var correctOptionId = mayRevealAnswer ? options.FirstOrDefault(o => o.IsCorrect)?.Id : null;

        var now = _clock.UtcNow;
        double? remaining = null;
        if (matchQuestion.TimeLimitSeconds is { } limit && matchQuestion.TimerStartedAtUtc is { } started)
        {
            remaining = Math.Max(0, limit - (now - started).TotalSeconds);
        }

        return new CurrentQuestionDto(
            matchQuestion.Id,
            matchQuestion.OrderIndex,
            matchQuestion.State.ToString(),
            question.FormatCode.ToString(),
            question.QuestionText,
            (byte)question.DifficultyLevel,
            topicName,
            null,
            orderedOptions.Count == 0 ? null : orderedOptions,
            correctOptionId,
            matchQuestion.TimeLimitSeconds,
            matchQuestion.TimerStartedAtUtc,
            now,
            remaining);
    }

    /// <summary>OptionOrderJson is the per-match shuffle fixed at reservation
    /// time; without one, options appear in their authored order.</summary>
    private static IEnumerable<Domain.QuestionBank.QuestionOption> OrderOptions(
        IReadOnlyList<Domain.QuestionBank.QuestionOption> options, string? optionOrderJson)
    {
        var order = string.IsNullOrWhiteSpace(optionOrderJson)
            ? null
            : JsonSerializer.Deserialize<List<Guid>>(optionOrderJson);
        if (order is null || order.Count == 0)
        {
            return options.OrderBy(o => o.DisplayOrder);
        }

        return options.OrderBy(o =>
        {
            var index = order.IndexOf(o.Id);
            return index < 0 ? int.MaxValue : index;
        });
    }
}
