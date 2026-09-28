using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using Quizware.Domain.Scoring;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Gameplay.Commands;

public sealed record RecordAnswerCommand(
    Guid MatchId,
    Guid MatchQuestionId,
    Guid MatchParticipantId,
    string Outcome,
    Guid? SelectedOptionId,
    IReadOnlyList<Guid>? SelectedOptionIds,
    string? FreeTextAnswer,
    int PassNumber,
    string? AnswerSource,
    Guid? BuzzPressId,
    int? ResponseTimeMs,
    string? IdempotencyKey) : IRequest<RecordAnswerResultDto>;

public sealed class RecordAnswerCommandValidator : AbstractValidator<RecordAnswerCommand>
{
    /// <summary>Passing, skipping, reversing and manual adjustments have their
    /// own endpoints; only an actual answer (or the lack of one) is recorded here.</summary>
    public static readonly AnswerOutcome[] RecordableOutcomes =
    [
        AnswerOutcome.Correct, AnswerOutcome.Incorrect, AnswerOutcome.NoAnswer, AnswerOutcome.TimedOut,
        AnswerOutcome.PassedCorrect, AnswerOutcome.PassedIncorrect,
    ];

    public RecordAnswerCommandValidator()
    {
        RuleFor(x => x.MatchQuestionId).NotEmpty();
        RuleFor(x => x.MatchParticipantId).NotEmpty();
        RuleFor(x => x.Outcome)
            .Must(o => Enum.TryParse<AnswerOutcome>(o, ignoreCase: true, out var parsed) && RecordableOutcomes.Contains(parsed))
            .WithMessage($"Outcome must be one of: {string.Join(", ", RecordableOutcomes)}.");
        RuleFor(x => x.AnswerSource)
            .Must(s => s is null || Enum.TryParse<AnswerSource>(s, ignoreCase: true, out _))
            .WithMessage($"AnswerSource must be one of: {string.Join(", ", Enum.GetNames<AnswerSource>())}.");
        RuleFor(x => x.PassNumber).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FreeTextAnswer).MaximumLength(1000);
        RuleFor(x => x.ResponseTimeMs).GreaterThanOrEqualTo(0).When(x => x.ResponseTimeMs is not null);
        RuleFor(x => x.IdempotencyKey).MaximumLength(100);
    }
}

/// <summary>One unit of work: the answer record, its score event (the
/// immutable ledger) and the running TeamMatchScore total. Outside a buzzer
/// segment only the team holding the question may answer; in a buzzer segment
/// any active team may, and — when the question allows a steal — a wrong
/// answer leaves it open to the rest until someone gets it or everyone has tried.</summary>
public sealed class RecordAnswerCommandHandler : IRequestHandler<RecordAnswerCommand, RecordAnswerResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ScoringResolver _scoring;
    private readonly MatchEventLog _eventLog;
    private readonly AnswerResultBuilder _results;
    private readonly LiveQuestionFormats _formats;

    public RecordAnswerCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, ScoringResolver scoring, MatchEventLog eventLog, AnswerResultBuilder results,
        LiveQuestionFormats formats)
    {
        _db = db;
        _currentUser = currentUser;
        _scoring = scoring;
        _eventLog = eventLog;
        _results = results;
        _formats = formats;
    }

    public async Task<RecordAnswerResultDto> Handle(RecordAnswerCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        LiveRules.RequireInProgress(match);
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to record an answer.");

        if (request.IdempotencyKey is not null
            && await _db.AnswerRecords.AnyAsync(a => a.ProgramId == match.ProgramId && a.IdempotencyKey == request.IdempotencyKey, cancellationToken))
        {
            throw new InvalidStateTransitionException("An answer with this Idempotency-Key has already been recorded.");
        }

        var question = await LiveRules.LoadQuestionAsync(_db, match.Id, request.MatchQuestionId, cancellationToken);
        if (question.State != MatchQuestionState.Active)
        {
            throw new InvalidStateTransitionException($"Question at position {question.OrderIndex} is {question.State}; only the question on screen can be answered.");
        }

        var segment = await _db.MatchSegments.SingleAsync(s => s.Id == question.MatchSegmentId, cancellationToken);
        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var participant = participants.SingleOrDefault(p => p.Id == request.MatchParticipantId)
            ?? throw new KeyNotFoundException($"Participant '{request.MatchParticipantId}' was not found in this match.");
        if (participant.Status != ParticipantStatus.Active)
        {
            throw new InvalidStateTransitionException($"This team is {participant.Status} and cannot answer.");
        }

        var priorOnQuestion = await _db.AnswerRecords
            .Where(a => a.MatchQuestionId == question.Id && !a.IsReversed && a.Outcome != AnswerOutcome.Voided)
            .ToListAsync(cancellationToken);
        var isBuzzer = segment.FormatCode == QuestionFormatCode.Buzzer;

        if (!isBuzzer && question.TargetParticipantId != participant.Id)
        {
            throw new InvalidStateTransitionException("Only the team holding this question may answer it.");
        }

        if (priorOnQuestion.Any(a => a.MatchParticipantId == participant.Id && a.Outcome != AnswerOutcome.Passed))
        {
            throw new InvalidStateTransitionException("This team has already answered this question.");
        }

        var passNumber = priorOnQuestion.Count(a => a.Outcome == AnswerOutcome.Passed);
        if (request.PassNumber != passNumber)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["passNumber"] = [$"This question has been passed {passNumber} time(s); PassNumber must be {passNumber}."],
            });
        }

        var outcome = NormalizeOutcome(Enum.Parse<AnswerOutcome>(request.Outcome, ignoreCase: true), passNumber);
        var isCorrect = await CheckResponseAsync(question, outcome, request, cancellationToken);

        var rule = await _scoring.ResolveAsync(match, segment, outcome, passNumber, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        var source = request.AnswerSource is null ? AnswerSource.Operator : Enum.Parse<AnswerSource>(request.AnswerSource, ignoreCase: true);

        var answer = AnswerRecord.Create(
            match.ProgramId, match.Id, segment.Id, question.Id, participant.TeamId, participant.Id, outcome, userId, actor,
            passNumber, source, request.IdempotencyKey);
        answer.RecordResponse(
            request.SelectedOptionId,
            request.SelectedOptionIds is { Count: > 0 } ids ? JsonSerializer.Serialize(ids) : null,
            request.FreeTextAnswer,
            isCorrect,
            request.BuzzPressId,
            request.ResponseTimeMs);
        _db.AnswerRecords.Add(answer);
        _db.ScoreEvents.Add(ScoreEvent.ForAnswer(
            match.ProgramId, match.Id, participant.TeamId, participant.Id, answer.Id, rule.Id, rule.Points, userId, segment.Id));

        var score = await _db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == participant.Id, cancellationToken);
        score.ApplyAnswer(outcome, rule.Points);

        var stealAllowed = isBuzzer && await _db.Questions.IgnoreQueryFilters()
            .OfType<BuzzerQuestion>()
            .Where(q => q.Id == question.QuestionId)
            .Select(q => q.AllowStealAfterWrong)
            .SingleOrDefaultAsync(cancellationToken);
        if (ClosesQuestion(stealAllowed, outcome, priorOnQuestion.Count(a => a.Outcome != AnswerOutcome.Passed) + 1, participants))
        {
            question.MarkAnswered();
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.AnswerRecorded, new
        {
            answerRecordId = answer.Id,
            matchQuestionId = question.Id,
            participantId = participant.Id,
            outcome = outcome.ToString(),
            points = rule.Points,
            scoringRuleId = rule.Id,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _results.BuildAsync(match, segment, answer, rule.Points, rule.Id, cancellationToken);
    }

    /// <summary>After a pass, a right/wrong answer is the "after pass" kind,
    /// which the rules score separately.</summary>
    private static AnswerOutcome NormalizeOutcome(AnswerOutcome outcome, int passNumber) => (outcome, passNumber > 0) switch
    {
        (AnswerOutcome.Correct, true) => AnswerOutcome.PassedCorrect,
        (AnswerOutcome.Incorrect, true) => AnswerOutcome.PassedIncorrect,
        (AnswerOutcome.PassedCorrect, false) or (AnswerOutcome.PassedIncorrect, false) =>
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["outcome"] = ["This question has not been passed; record Correct or Incorrect."],
            }),
        _ => outcome,
    };

    /// <summary>A wrong buzzer answer leaves the question open to the other
    /// teams only when the question allows a steal.</summary>
    private static bool ClosesQuestion(
        bool stealAllowed, AnswerOutcome outcome, int answersOnQuestion, IReadOnlyList<Domain.Tournament.MatchParticipant> participants)
    {
        if (!stealAllowed || outcome is AnswerOutcome.Correct or AnswerOutcome.PassedCorrect)
        {
            return true;
        }

        return answersOnQuestion >= participants.Count(p => p.Status == ParticipantStatus.Active);
    }

    /// <summary>When the response can be checked objectively (options, a
    /// sequence order), the stated outcome must agree with it — a mis-click is
    /// refused, not scored.</summary>
    private async Task<bool?> CheckResponseAsync(
        MatchQuestion matchQuestion, AnswerOutcome outcome, RecordAnswerCommand request, CancellationToken cancellationToken)
    {
        var selected = request.SelectedOptionIds is { Count: > 0 } many
            ? many.ToList()
            : request.SelectedOptionId is { } one ? [one] : null;
        var question = await _db.Questions.IgnoreQueryFilters().SingleAsync(q => q.Id == matchQuestion.QuestionId, cancellationToken);
        var evaluation = await _formats.EvaluateAsync(question, selected, request.FreeTextAnswer, cancellationToken);

        if (evaluation.IsObjective && evaluation.IsCorrect is { } isCorrect)
        {
            var claimsCorrect = outcome is AnswerOutcome.Correct or AnswerOutcome.PassedCorrect;
            var claimsIncorrect = outcome is AnswerOutcome.Incorrect or AnswerOutcome.PassedIncorrect;
            if ((claimsCorrect && !isCorrect) || (claimsIncorrect && isCorrect))
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["outcome"] = [$"Outcome {outcome} contradicts the team's response, which is {(isCorrect ? "correct" : "incorrect")}."],
                });
            }
        }

        return evaluation.IsCorrect;
    }
}
