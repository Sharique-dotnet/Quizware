using FluentValidation;
using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record AbandonMatchCommand(Guid MatchId, string Reason) : IRequest<LiveMatchStateDto>;

public sealed class AbandonMatchCommandValidator : AbstractValidator<AbandonMatchCommand>
{
    public AbandonMatchCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Stops a match without a result. Nothing is deleted: answers and
/// scores stay for the record, and unserved questions return to the pool.</summary>
public sealed class AbandonMatchCommandHandler : IRequestHandler<AbandonMatchCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchCompletion _completion;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public AbandonMatchCommandHandler(
        IAppDbContext db, MatchCompletion completion, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _completion = completion;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(AbandonMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        match.Abandon(request.Reason);
        await _completion.CloseOutstandingWorkAsync(match, "Match abandoned", cancellationToken);
        await _eventLog.AppendAsync(match, MatchEventTypes.MatchAbandoned, new { reason = request.Reason }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await _state.BuildAsync(match, cancellationToken);
    }
}
