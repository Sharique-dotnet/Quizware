using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record ParticipantOrderEntry(Guid ParticipantId, int SeatNumber, int TurnOrder);

public sealed record OrderMatchParticipantsCommand(
    Guid ProgramId, Guid MatchId, IReadOnlyList<ParticipantOrderEntry> Participants) : IRequest<IReadOnlyList<ParticipantDto>>;

public sealed class OrderMatchParticipantsCommandValidator : AbstractValidator<OrderMatchParticipantsCommand>
{
    public OrderMatchParticipantsCommandValidator()
    {
        RuleFor(x => x.Participants).NotEmpty();
        RuleForEach(x => x.Participants).ChildRules(entry =>
        {
            entry.RuleFor(e => e.SeatNumber).GreaterThan(0);
            entry.RuleFor(e => e.TurnOrder).GreaterThan(0);
        });
    }
}

/// <summary>Takes every participant exactly once. Seats must be distinct and
/// turn orders must be exactly 1..N, so the rotation never has a gap.</summary>
public sealed class OrderMatchParticipantsCommandHandler
    : IRequestHandler<OrderMatchParticipantsCommand, IReadOnlyList<ParticipantDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public OrderMatchParticipantsCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ParticipantDto>> Handle(
        OrderMatchParticipantsCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);
        var actor = _currentUser.Email ?? "unknown";
        match.TouchSetup(actor);

        var participants = await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken);
        var entries = request.Participants;
        var errors = new Dictionary<string, string[]>();

        if (entries.Count != participants.Count
            || entries.Select(e => e.ParticipantId).Distinct().Count() != entries.Count
            || entries.Any(e => participants.All(p => p.Id != e.ParticipantId)))
        {
            errors["participants"] = ["Must list every participant of this match exactly once."];
        }

        if (entries.Select(e => e.SeatNumber).Distinct().Count() != entries.Count)
        {
            errors["seatNumber"] = ["Seat numbers must be distinct."];
        }

        if (!entries.Select(e => e.TurnOrder).Order().SequenceEqual(Enumerable.Range(1, entries.Count)))
        {
            errors["turnOrder"] = [$"Turn orders must be exactly 1..{entries.Count}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        // Two-phase (D-023): the unique (MatchId, SeatNumber) index would
        // otherwise be violated mid-batch when two teams swap seats.
        var byId = participants.ToDictionary(p => p.Id);
        for (var i = 0; i < entries.Count; i++)
        {
            byId[entries[i].ParticipantId].SetSeatAndTurn(100_000 + i, entries[i].TurnOrder, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var entry in entries)
        {
            byId[entry.ParticipantId].SetSeatAndTurn(entry.SeatNumber, entry.TurnOrder, actor);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var teamNames = await MatchSetup.TeamNamesAsync(_db, participants.Select(p => p.TeamId), cancellationToken);
        return participants.OrderBy(p => p.SeatNumber).Select(p => MatchSetup.ToDto(p, teamNames)).ToList();
    }
}
