using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Gameplay.Commands;

public sealed record UpdateMatchCommand(Guid ProgramId, Guid MatchId, string? Name, int MatchNumber) : IRequest<MatchDto>;

public sealed class UpdateMatchCommandValidator : AbstractValidator<UpdateMatchCommand>
{
    public UpdateMatchCommandValidator()
    {
        RuleFor(x => x.Name).MaximumLength(150);
        RuleFor(x => x.MatchNumber).GreaterThan(0);
    }
}

public sealed class UpdateMatchCommandHandler : IRequestHandler<UpdateMatchCommand, MatchDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateMatchCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MatchDto> Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, request.ProgramId, request.MatchId, cancellationToken);

        var numberTaken = await _db.Matches.AnyAsync(
            m => m.StageId == match.StageId && m.MatchNumber == request.MatchNumber && m.Id != match.Id,
            cancellationToken);
        if (numberTaken)
        {
            throw new InvalidStateTransitionException($"This stage already has a match numbered {request.MatchNumber}.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        match.UpdateDetails(name, request.MatchNumber, _currentUser.Email ?? "unknown");
        await _db.SaveChangesAsync(cancellationToken);

        return await MatchSetup.ToDetailAsync(_db, match, cancellationToken);
    }
}
