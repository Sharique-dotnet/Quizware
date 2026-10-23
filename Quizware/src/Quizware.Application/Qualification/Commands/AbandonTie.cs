using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Qualification.Commands;

public sealed record AbandonTieCommand(Guid ProgramId, Guid TieId, string Reason) : IRequest;

public sealed class AbandonTieCommandValidator : AbstractValidator<AbandonTieCommand>
{
    public AbandonTieCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty();
    }
}

/// <summary>Cancels a tie that no longer needs settling (a team withdrew, the
/// standings were corrected). An unstarted tie-break match for it is
/// withdrawn; a match being played must be finished or abandoned first. If
/// the tie still exists, the next detection records it afresh.</summary>
public sealed class AbandonTieCommandHandler : IRequestHandler<AbandonTieCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TieBreakMatches _tieBreaks;

    public AbandonTieCommandHandler(IAppDbContext db, ICurrentUser currentUser, TieBreakMatches tieBreaks)
    {
        _db = db;
        _currentUser = currentUser;
        _tieBreaks = tieBreaks;
    }

    public async Task Handle(AbandonTieCommand request, CancellationToken cancellationToken)
    {
        var actor = _currentUser.Email ?? "unknown";
        var tie = await TieViews.LoadAsync(_db, request.ProgramId, request.TieId, cancellationToken);
        if (!tie.IsOpen)
        {
            throw new TieBreakNotApplicableException($"This tie is already {tie.State}.");
        }

        await _tieBreaks.WithdrawMatchAsync(tie, actor, cancellationToken);
        tie.Abandon(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
