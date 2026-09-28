using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Formats;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;
using Quizware.Application.Scoring;

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

/// <summary>P9-08: the team holding the question passes it on. Passing is
/// allowed in a segment that allows it, or for a question that carries its own
/// pass rules (the Passing format). The question's MaxPassCount and the
/// segment's, when both are set, the lower one wins. It goes to the next
/// active team by seat in the pass direction (BR-2.5, clockwise = ascending
/// seat) that has not yet held it. When every team has passed and the
/// question says RevealAnswerIfAllPass, the answer is revealed and the
/// question closes unanswered. A pass is scored only if a Passed rule exists.</summary>
public sealed class PassQuestionCommandHandler : IRequestHandler<PassQuestionCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScoringEngine _scoring;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;
    private readonly QuestionFormatHandlers _formats;

    public PassQuestionCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, IScoringEngine scoring, MatchEventLog eventLog, LiveStateBuilder state,
        QuestionFormatHandlers formats)
    {
        _db = db;
        _currentUser = currentUser;
        _scoring = scoring;
        _eventLog = eventLog;
        _state = state;
        _formats = formats;
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
        var bankQuestion = await _db.Questions.IgnoreQueryFilters().SingleAsync(q => q.Id == question.QuestionId, cancellationToken);
        var questionRules = _formats.For(segment.FormatCode).PassRules(bankQuestion);
        if (questionRules is null && template is not { AllowPassing: true })
        {
            throw new InvalidStateTransitionException("This segment does not allow passing.");
        }

        var limits = new[] { questionRules?.MaxPassCount, template?.MaxPassCount }.Where(l => l is not null).Select(l => l!.Value).ToList();
        int? maxPasses = limits.Count == 0 ? null : limits.Min();
        var direction = questionRules?.Direction ?? PassDirection.Clockwise;

        var passes = await _db.AnswerRecords
            .Where(a => a.MatchQuestionId == question.Id && a.Outcome == AnswerOutcome.Passed && !a.IsReversed)
            .ToListAsync(cancellationToken);
        if (maxPasses is { } max && passes.Count >= max)
        {
            throw new InvalidStateTransitionException($"This question has already been passed the maximum {max} time(s).");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var from = participants.Single(p => p.Id == request.FromParticipantId);
        var alreadyHeld = passes.Select(p => p.MatchParticipantId).Append(from.Id).ToHashSet();
        var to = NextBySeat(participants, from, direction).FirstOrDefault(p => !alreadyHeld.Contains(p.Id));
        var revealAfterAllPass = to is null && questionRules is { RevealAnswerIfAllPass: true };
        if (to is null && !revealAfterAllPass)
        {
            throw new InvalidStateTransitionException("Every other team has already held this question; it cannot be passed again.");
        }

        var pass = AnswerRecord.Create(
            match.ProgramId, match.Id, segment.Id, question.Id, from.TeamId, from.Id, AnswerOutcome.Passed, userId,
            _currentUser.Email ?? "unknown", passes.Count);
        _db.AnswerRecords.Add(pass);
        var points = await _scoring.ScorePassAsync(match, segment, from, pass, userId, cancellationToken);

        if (to is not null)
        {
            question.AssignTarget(to.TeamId, to.Id);
        }
        else
        {
            question.Reveal();
            question.Skip();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.QuestionPassed, new
        {
            matchQuestionId = question.Id,
            fromParticipantId = from.Id,
            toParticipantId = to?.Id,
            passNumber = passes.Count + 1,
            direction = direction.ToString(),
            revealedAfterAllPassed = revealAfterAllPass,
            points,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }

    /// <summary>The other active teams, in seat order going round the table
    /// from <paramref name="from"/>: ascending seats for clockwise, descending
    /// for anticlockwise. OperatorChoice falls back to clockwise — the pass
    /// request has no field to name the receiving team.</summary>
    private static IEnumerable<MatchParticipant> NextBySeat(
        IReadOnlyList<MatchParticipant> participants, MatchParticipant from, PassDirection direction)
    {
        var active = participants.Where(p => p.Status == ParticipantStatus.Active && p.Id != from.Id).ToList();
        return direction == PassDirection.Anticlockwise
            ? active.Where(p => p.SeatNumber < from.SeatNumber).OrderByDescending(p => p.SeatNumber)
                .Concat(active.Where(p => p.SeatNumber > from.SeatNumber).OrderByDescending(p => p.SeatNumber))
            : active.Where(p => p.SeatNumber > from.SeatNumber).OrderBy(p => p.SeatNumber)
                .Concat(active.Where(p => p.SeatNumber < from.SeatNumber).OrderBy(p => p.SeatNumber));
    }
}
