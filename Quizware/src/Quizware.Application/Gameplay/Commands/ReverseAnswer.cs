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

public sealed record ReverseAnswerCommand(Guid MatchId, Guid AnswerRecordId, string Reason) : IRequest<RecordAnswerResultDto>;

public sealed class ReverseAnswerCommandValidator : AbstractValidator<ReverseAnswerCommand>
{
    public ReverseAnswerCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Undo without erasing: a Voided compensating answer, an
/// opposite-signed score event, and the running total wound back. If nothing
/// has happened on stage since, the question goes back on screen so the right
/// outcome can be recorded. Passes are not reversible — the question has
/// already moved to another team.</summary>
public sealed class ReverseAnswerCommandHandler : IRequestHandler<ReverseAnswerCommand, RecordAnswerResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly MatchEventLog _eventLog;
    private readonly AnswerResultBuilder _results;

    public ReverseAnswerCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, MatchEventLog eventLog, AnswerResultBuilder results)
    {
        _db = db;
        _currentUser = currentUser;
        _eventLog = eventLog;
        _results = results;
    }

    public async Task<RecordAnswerResultDto> Handle(ReverseAnswerCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        if (match.State is not (MatchState.InProgress or MatchState.Paused))
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} is {match.State}; answers can only be reversed during play.");
        }

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to reverse an answer.");
        var original = await _db.AnswerRecords
            .SingleOrDefaultAsync(a => a.Id == request.AnswerRecordId && a.MatchId == match.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Answer '{request.AnswerRecordId}' was not found in this match.");

        if (original.IsReversed || original.Outcome == AnswerOutcome.Voided)
        {
            throw new InvalidStateTransitionException("This answer has already been reversed.");
        }

        if (original.Outcome == AnswerOutcome.Passed)
        {
            throw new InvalidStateTransitionException("A pass cannot be reversed; the question has already moved to another team.");
        }

        var scoreEvent = await _db.ScoreEvents
            .SingleOrDefaultAsync(e => e.AnswerRecordId == original.Id && e.EventType == ScoreEventType.Answer, cancellationToken)
            ?? throw new InvalidStateTransitionException("This answer has no score event to reverse.");

        var compensating = AnswerRecord.Create(
            original.ProgramId, match.Id, original.MatchSegmentId, original.MatchQuestionId, original.TeamId,
            original.MatchParticipantId, AnswerOutcome.Voided, userId, _currentUser.Email ?? "unknown", original.PassNumber);
        _db.AnswerRecords.Add(compensating);
        original.MarkReversed(compensating.Id, request.Reason);
        _db.ScoreEvents.Add(scoreEvent.Reverse(userId, request.Reason));

        var score = await _db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == original.MatchParticipantId, cancellationToken);
        score.RevertAnswer(original.Outcome, scoreEvent.Points);

        var question = await _db.MatchQuestions.SingleAsync(q => q.Id == original.MatchQuestionId, cancellationToken);
        var reopened = await ReopenIfNothingHappenedSinceAsync(match.Id, question, cancellationToken);

        await _eventLog.AppendAsync(match, MatchEventTypes.AnswerReversed, new
        {
            answerRecordId = original.Id,
            compensatingAnswerRecordId = compensating.Id,
            points = -scoreEvent.Points,
            reason = request.Reason,
            questionReopened = reopened,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var segment = await _db.MatchSegments.SingleAsync(s => s.Id == original.MatchSegmentId, cancellationToken);
        return await _results.BuildAsync(match, segment, compensating, -scoreEvent.Points, scoreEvent.ScoringRuleId!.Value, cancellationToken);
    }

    private async Task<bool> ReopenIfNothingHappenedSinceAsync(Guid matchId, MatchQuestion question, CancellationToken cancellationToken)
    {
        if (question.State != MatchQuestionState.Answered)
        {
            return false;
        }

        var anythingSince = await _db.MatchQuestions.AnyAsync(
            q => q.MatchId == matchId && q.Id != question.Id
                && (q.State == MatchQuestionState.Active || q.ServedAtUtc > question.ServedAtUtc),
            cancellationToken);
        var segmentStillOpen = await _db.MatchSegments.AnyAsync(
            s => s.Id == question.MatchSegmentId && s.State == MatchSegmentState.Open, cancellationToken);
        if (anythingSince || !segmentStillOpen)
        {
            return false;
        }

        question.Reopen();
        return true;
    }
}
