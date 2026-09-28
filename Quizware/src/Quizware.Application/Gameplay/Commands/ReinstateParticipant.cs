using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Application.Gameplay.Commands;

public sealed record ReinstateParticipantCommand(Guid MatchId, Guid ParticipantId, string Reason) : IRequest<LiveMatchStateDto>;

public sealed class ReinstateParticipantCommandValidator : AbstractValidator<ReinstateParticipantCommand>
{
    public ReinstateParticipantCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Brings a disqualified team back while the match is still being
/// played. It rejoins at the end of the rotation; its score was never lost.</summary>
public sealed class ReinstateParticipantCommandHandler : IRequestHandler<ReinstateParticipantCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public ReinstateParticipantCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(ReinstateParticipantCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        if (match.State is not (MatchState.InProgress or MatchState.Paused))
        {
            throw new InvalidStateTransitionException($"Match {match.MatchNumber} is {match.State}; a team can only be reinstated during play.");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var participant = participants.SingleOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new KeyNotFoundException($"Participant '{request.ParticipantId}' was not found in this match.");
        if (participant.Status != ParticipantStatus.Disqualified)
        {
            throw new InvalidStateTransitionException($"This team is {participant.Status}, not disqualified.");
        }

        var lastTurn = participants.Where(p => p.Status == ParticipantStatus.Active).Select(p => p.TurnOrder).DefaultIfEmpty(0).Max();
        participant.Reinstate(lastTurn + 1);
        MatchSetup.RecompactTurnOrder(participants);

        await _eventLog.AppendAsync(match, MatchEventTypes.ParticipantReinstated, new { participantId = participant.Id, reason = request.Reason }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await _state.BuildAsync(match, cancellationToken);
    }
}
