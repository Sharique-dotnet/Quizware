using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Formats;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Application.Gameplay.Commands;

public sealed record ServeQuestionCommand(Guid MatchId, Guid SegmentId) : IRequest<CurrentQuestionDto>;

/// <summary>Puts the segment's next reserved question on screen, aimed at the
/// team whose turn it is (TurnOrderCalculator over active teams only — the
/// replacement for the legacy QuestionNumber % 3), and records the usage that
/// drives the repeat policy for later draws.</summary>
public sealed class ServeQuestionCommandHandler : IRequestHandler<ServeQuestionCommand, CurrentQuestionDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;
    private readonly QuestionFormatHandlers _formats;

    public ServeQuestionCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state, QuestionFormatHandlers formats)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
        _formats = formats;
    }

    public async Task<CurrentQuestionDto> Handle(ServeQuestionCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var segment = segments.SingleOrDefault(s => s.Id == request.SegmentId)
            ?? throw new KeyNotFoundException($"Segment '{request.SegmentId}' was not found in this match.");
        if (segment.State != MatchSegmentState.Open)
        {
            throw new InvalidStateTransitionException($"Segment {segment.OrderIndex} is {segment.State}; open it before serving questions.");
        }

        if (await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken) is not null)
        {
            throw new InvalidStateTransitionException("A question is already on screen — answer or skip it first.");
        }

        var next = (await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken)).FirstOrDefault()
            ?? throw new InvalidStateTransitionException($"Segment {segment.OrderIndex} has no questions left to serve; close it.");

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var handler = _formats.For(segment.FormatCode);
        var targetId = TurnRotation.NextParticipantOrNull(participants, segment, _formats);
        if (targetId is null && !handler.AnyTeamMayAnswer)
        {
            throw new NoActiveParticipantsException();
        }

        var target = targetId is null ? null : participants.Single(p => p.Id == targetId);

        var question = await _db.Questions.IgnoreQueryFilters().SingleAsync(q => q.Id == next.QuestionId, cancellationToken);
        var template = await _db.StageSegmentTemplates.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == segment.SegmentTemplateId, cancellationToken);
        var timeLimit = question.TimeLimitSeconds ?? template?.TimeLimitSeconds ?? handler.DefaultTimeLimitSeconds(question);
        var segmentQuestions = await _db.MatchQuestions.Where(q => q.MatchSegmentId == segment.Id).ToListAsync(cancellationToken);

        next.AssignTarget(target?.TeamId, target?.Id);
        next.Activate(segmentQuestions, next.OptionOrderJson ?? "[]", timeLimit);
        segment.RecordQuestionServed();
        question.RecordUsage();
        _db.QuestionUsageHistories.Add(QuestionUsageHistory.Record(match.ProgramId, question.Id, match.Id, match.StageId, target?.TeamId));

        await _eventLog.AppendAsync(match, MatchEventTypes.QuestionServed, new
        {
            matchQuestionId = next.Id,
            questionId = question.Id,
            segmentId = segment.Id,
            participantId = target?.Id,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.PresentAsync(next, cancellationToken);
    }
}
