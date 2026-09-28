using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Application.Gameplay.Formats;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;

namespace Quizware.Application.Gameplay.Commands;

public sealed record DisqualifyParticipantCommand(
    Guid MatchId, Guid ParticipantId, string Reason, Guid ApprovedByUserId, bool ExcludeFromStandings) : IRequest<DisqualifyResultDto>;

public sealed class DisqualifyParticipantCommandValidator : AbstractValidator<DisqualifyParticipantCommand>
{
    public DisqualifyParticipantCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ApprovedByUserId).NotEmpty();
    }
}

/// <summary>The legacy system kept a removed team in the QuestionNumber % 3
/// rotation and filled its turns with fake answers. Here the team leaves the
/// rotation outright: turn order is recompacted over the teams still playing,
/// a question it was holding is skipped (never answered on its behalf), the
/// open segment is adjusted per the stage's TeamCountChangePolicy, and if
/// fewer than two teams remain the match completes on the spot.</summary>
public sealed class DisqualifyParticipantCommandHandler : IRequestHandler<DisqualifyParticipantCommand, DisqualifyResultDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly MatchCompletion _completion;
    private readonly QuestionFormatHandlers _formats;

    public DisqualifyParticipantCommandHandler(
        IAppDbContext db, MatchEventLog eventLog, MatchCompletion completion, QuestionFormatHandlers formats)
    {
        _db = db;
        _eventLog = eventLog;
        _completion = completion;
        _formats = formats;
    }

    public async Task<DisqualifyResultDto> Handle(DisqualifyParticipantCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        if (match.State is not (MatchState.InProgress or MatchState.Paused))
        {
            throw new InvalidStateTransitionException(
                $"Match {match.MatchNumber} is {match.State}; disqualification only applies during play (remove the team in setup instead).");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var participant = participants.SingleOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new KeyNotFoundException($"Participant '{request.ParticipantId}' was not found in this match.");
        if (participant.Status != ParticipantStatus.Active)
        {
            throw new InvalidStateTransitionException($"This team is already {participant.Status}.");
        }

        var segments = await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken);
        var openSegment = segments.SingleOrDefault(s => s.State == MatchSegmentState.Open);
        participant.Disqualify(request.Reason, request.ApprovedByUserId, openSegment?.Id, request.ExcludeFromStandings);

        var active = await LiveRules.ActiveQuestionAsync(_db, match.Id, cancellationToken);
        var skippedQuestionId = active?.TargetParticipantId == participant.Id ? active.Id : (Guid?)null;
        if (skippedQuestionId is not null)
        {
            active!.Skip();
        }

        var remaining = participants.Where(p => p.Status == ParticipantStatus.Active).ToList();
        MatchSetup.RecompactTurnOrder(remaining);

        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        CurrentSegmentAdjustmentDto? adjustment = null;
        if (openSegment is not null && remaining.Count >= 2)
        {
            adjustment = await AdjustOpenSegmentAsync(openSegment, stage.TeamCountChangePolicy, remaining.Count, cancellationToken);
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.ParticipantDisqualified, new
        {
            participantId = participant.Id,
            reason = request.Reason,
            approvedByUserId = request.ApprovedByUserId,
            excludeFromStandings = request.ExcludeFromStandings,
            skippedMatchQuestionId = skippedQuestionId,
            segmentAdjustment = adjustment,
        }, cancellationToken);

        var matchCanContinue = remaining.Count >= 2;
        if (!matchCanContinue)
        {
            if (match.State == MatchState.Paused)
            {
                match.Resume();
            }

            await _completion.CompleteAsync(match, "Fewer than two teams remain", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var teamNames = await MatchSetup.TeamNamesAsync(_db, participants.Select(p => p.TeamId), cancellationToken);
        var scores = await _db.TeamMatchScores
            .Where(s => s.MatchId == match.Id)
            .ToDictionaryAsync(s => s.MatchParticipantId, s => s.TotalPoints, cancellationToken);

        return new DisqualifyResultDto(
            participant.Id,
            teamNames.GetValueOrDefault(participant.TeamId, string.Empty),
            participant.Status.ToString(),
            participant.RemovedAtUtc!.Value,
            remaining
                .OrderBy(p => p.TurnOrder)
                .Select(p => new LiveParticipantScoreDto(
                    p.Id, teamNames.GetValueOrDefault(p.TeamId, string.Empty), p.SeatNumber, p.TurnOrder, p.Status.ToString(),
                    scores.GetValueOrDefault(p.Id)))
                .ToList(),
            matchCanContinue,
            TurnOrderRecalculated: true,
            adjustment,
            matchCanContinue && openSegment is not null ? TurnRotation.NextParticipantOrNull(participants, openSegment, _formats) : null,
            match.State == MatchState.Completed,
            match.WinnerTeamId);
    }

    /// <summary>KeepPlanned: nothing changes. Rebalance: the questions still to
    /// come are trimmed to a multiple of the remaining teams, so everyone gets
    /// the same number of turns. Truncate: the segment ends after the questions
    /// already served. Trimmed questions go back to the pool.</summary>
    private async Task<CurrentSegmentAdjustmentDto> AdjustOpenSegmentAsync(
        MatchSegment segment, TeamCountChangePolicy policy, int remainingTeams, CancellationToken cancellationToken)
    {
        var before = segment.PlannedQuestionCount;
        var served = segment.ServedQuestionCount;
        var after = policy switch
        {
            TeamCountChangePolicy.Rebalance => served + ((before - served) / remainingTeams * remainingTeams),
            TeamCountChangePolicy.Truncate => served,
            _ => before,
        };

        if (after != before)
        {
            segment.AdjustPlannedQuestionCount(after);
            var reserved = await LiveRules.ReservedInSegmentAsync(_db, segment.Id, cancellationToken);
            var keep = after - served;
            foreach (var question in reserved.Skip(Math.Max(0, keep)))
            {
                question.Release();
            }
        }

        var message = policy switch
        {
            TeamCountChangePolicy.Rebalance when after != before =>
                $"{before - after} question(s) dropped so each of the {remainingTeams} remaining teams gets an equal number of turns.",
            TeamCountChangePolicy.Truncate when after != before => "The segment ends after the questions already served.",
            _ => "The segment keeps its planned question count.",
        };

        return new CurrentSegmentAdjustmentDto(policy.ToString(), before, after, message);
    }
}
