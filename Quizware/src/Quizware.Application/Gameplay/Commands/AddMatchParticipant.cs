using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay.Commands;

public sealed record AddMatchParticipantCommand(Guid ProgramId, Guid MatchId, Guid TeamId, int SeatNumber) : IRequest<ParticipantDto>;

public sealed class AddMatchParticipantCommandValidator : AbstractValidator<AddMatchParticipantCommand>
{
    public AddMatchParticipantCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.SeatNumber).GreaterThan(0);
    }
}

/// <summary>The new participant takes the next turn after everyone already
/// seated; PUT participants/order changes it.</summary>
public sealed class AddMatchParticipantCommandHandler : IRequestHandler<AddMatchParticipantCommand, ParticipantDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AddMatchParticipantCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ParticipantDto> Handle(AddMatchParticipantCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        var team = await _db.Teams
            .SingleOrDefaultAsync(t => t.Id == request.TeamId && t.ProgramId == request.ProgramId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");
        if (team.Status is TeamStatus.Withdrawn or TeamStatus.Disqualified)
        {
            throw new InvalidStateTransitionException($"Team '{team.DisplayName}' is {team.Status} and cannot join a match.");
        }

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        if (participants.Any(p => p.TeamId == team.Id))
        {
            throw new InvalidStateTransitionException($"Team '{team.DisplayName}' is already in this match.");
        }

        if (participants.Any(p => p.SeatNumber == request.SeatNumber))
        {
            throw new InvalidStateTransitionException($"Seat {request.SeatNumber} is already taken in this match.");
        }

        var stage = await _db.Stages.SingleAsync(s => s.Id == match.StageId, cancellationToken);
        if (participants.Count >= stage.MaxTeamsPerMatch)
        {
            throw new InvalidStateTransitionException(
                $"Stage '{stage.Name}' allows at most {stage.MaxTeamsPerMatch} teams per match.");
        }

        var turnOrder = participants.Count == 0 ? 1 : participants.Max(p => p.TurnOrder) + 1;
        var participant = MatchParticipant.Create(
            match.ProgramId, match.Id, team.Id, request.SeatNumber, turnOrder, actor);
        _db.MatchParticipants.Add(participant);
        await _db.SaveChangesAsync(cancellationToken);

        return MatchSetup.ToDto(participant, new Dictionary<Guid, string> { [team.Id] = team.DisplayName });
    }
}
