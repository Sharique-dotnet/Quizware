using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Scoring;

namespace Quizware.Application.Gameplay.Commands;

public sealed record PassQuestionCommand(Guid MatchId, Guid MatchQuestionId, Guid FromParticipantId) : IRequest<LiveMatchStateDto>;

public sealed class PassQuestionCommandValidator : AbstractValidator<PassQuestionCommand>
{
    public PassQuestionCommandValidator()
    {
        RuleFor(x => x.MatchQuestionId).NotEmpty();
        RuleFor(x => x.FromParticipantId).NotEmpty();
    }
}

/// <summary>The team holding the question passes it on to the next active
/// team in turn order that has not held it yet. Only in a segment that allows
/// passing, at most MaxPassCount times. A pass is scored only if a rule for
/// the Passed outcome exists — an unconfigured pass simply carries no points.</summary>
public sealed class PassQuestionCommandHandler : IRequestHandler<PassQuestionCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ScoringResolver _scoring;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public PassQuestionCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, ScoringResolver scoring, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _currentUser = currentUser;
        _scoring = scoring;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(PassQuestionCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to pass a question.");

        var question = await LiveRules.LoadQuestionAsync(_db, match.Id, request.MatchQuestionId, cancellationToken);
        if (question.State != MatchQuestionState.Active)
        {
            throw new InvalidStateTransitionException($"Question at position {question.OrderIndex} is {question.State}; only the question on screen can be passed.");
        }

        if (question.TargetParticipantId != request.FromParticipantId)
        {
            throw new InvalidStateTransitionException("Only the team holding the question may pass it.");
        }

        var segment = await _db.MatchSegments.SingleAsync(s => s.Id == question.MatchSegmentId, cancellationToken);
        var template = await _db.StageSegmentTemplates.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Id == segment.SegmentTemplateId, cancellationToken);
        if (template is not { AllowPassing: true })
        {
            throw new InvalidStateTransitionException("This segment does not allow passing.");
        }

        var passes = await _db.AnswerRecords
            .Where(a => a.MatchQuestionId == question.Id && a.Outcome == AnswerOutcome.Passed && !a.IsReversed)
            .ToListAsync(cancellationToken);
        if (template.MaxPassCount is { } max && passes.Count >= max)
        {
            throw new InvalidStateTransitionException($"This question has already been passed the maximum {max} time(s).");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var from = participants.Single(p => p.Id == request.FromParticipantId);
        var alreadyHeld = passes.Select(p => p.MatchParticipantId).Append(from.Id).ToHashSet();
        var active = participants.Where(p => p.Status == ParticipantStatus.Active).OrderBy(p => p.TurnOrder).ToList();
        var to = active.Where(p => p.TurnOrder > from.TurnOrder).Concat(active.Where(p => p.TurnOrder < from.TurnOrder))
            .FirstOrDefault(p => !alreadyHeld.Contains(p.Id))
            ?? throw new InvalidStateTransitionException("Every other team has already held this question; it cannot be passed again.");

        var rule = await _scoring.TryResolveAsync(match, segment, AnswerOutcome.Passed, passes.Count, cancellationToken);
        var points = rule?.Points ?? 0;
        var pass = AnswerRecord.Create(
            match.ProgramId, match.Id, segment.Id, question.Id, from.TeamId, from.Id, AnswerOutcome.Passed, userId,
            _currentUser.Email ?? "unknown", passes.Count);
        _db.AnswerRecords.Add(pass);
        if (rule is not null)
        {
            _db.ScoreEvents.Add(ScoreEvent.ForAnswer(
                match.ProgramId, match.Id, from.TeamId, from.Id, pass.Id, rule.Id, points, userId, segment.Id));
        }

        var score = await _db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == from.Id, cancellationToken);
        score.ApplyAnswer(AnswerOutcome.Passed, points);
        question.AssignTarget(to.TeamId, to.Id);

        await _eventLog.AppendAsync(match, MatchEventTypes.QuestionPassed, new
        {
            matchQuestionId = question.Id,
            fromParticipantId = from.Id,
            toParticipantId = to.Id,
            passNumber = passes.Count + 1,
            points,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
