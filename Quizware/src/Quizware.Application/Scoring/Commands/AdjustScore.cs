using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Application.Scoring.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Scoring.Commands;

public sealed record AdjustScoreCommand(Guid MatchId, Guid TeamId, int PointsDelta, string Reason) : IRequest<IReadOnlyList<TeamScoreItemDto>>;

public sealed class AdjustScoreCommandValidator : AbstractValidator<AdjustScoreCommand>
{
    public AdjustScoreCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.PointsDelta).NotEqual(0).WithMessage("An adjustment must change the score.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>P10-03: a ProgramAdmin's manual correction (the policy is
/// enforced on the route). It is an ordinary ScoreEvent with a mandatory
/// reason, so the ledger explains every point. On a completed match the
/// result is re-ranked and the stage record rebuilt, since the winner may
/// have changed.</summary>
public sealed class AdjustScoreCommandHandler : IRequestHandler<AdjustScoreCommand, IReadOnlyList<TeamScoreItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScoringEngine _scoring;
    private readonly MatchCompletion _completion;
    private readonly MatchEventLog _eventLog;

    public AdjustScoreCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, IScoringEngine scoring, MatchCompletion completion, MatchEventLog eventLog)
    {
        _db = db;
        _currentUser = currentUser;
        _scoring = scoring;
        _completion = completion;
        _eventLog = eventLog;
    }

    public async Task<IReadOnlyList<TeamScoreItemDto>> Handle(AdjustScoreCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchScoreboard.LoadMatchAsync(_db, request.MatchId, cancellationToken);
        if (match.State is not (MatchState.InProgress or MatchState.Paused or MatchState.Completed))
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} is {match.State}; only a played match's score can be adjusted.");
        }

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("An authenticated user is required to adjust a score.");
        var participant = await _db.MatchParticipants
            .SingleOrDefaultAsync(p => p.MatchId == match.Id && p.TeamId == request.TeamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' did not play in this match.");

        var adjustment = await _scoring.AdjustAsync(match, participant, request.PointsDelta, request.Reason, userId, cancellationToken);

        if (match.State == MatchState.Completed)
        {
            await _completion.ReviseResultAsync(match, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _scoring.RecalculateStageAsync(match.StageId, cancellationToken);
        }

        await _eventLog.AppendAsync(match, MatchEventTypes.ScoreAdjusted, new
        {
            scoreEventId = adjustment.Id,
            teamId = request.TeamId,
            points = request.PointsDelta,
            reason = request.Reason,
        }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await MatchScoreboard.BuildAsync(_db, match, cancellationToken);
    }
}
