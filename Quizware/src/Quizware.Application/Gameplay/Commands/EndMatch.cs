using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record EndMatchCommand(Guid MatchId) : IRequest<LiveMatchStateDto>;

public sealed class EndMatchCommandHandler : IRequestHandler<EndMatchCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchCompletion _completion;
    private readonly LiveStateBuilder _state;

    public EndMatchCommandHandler(IAppDbContext db, MatchCompletion completion, LiveStateBuilder state)
    {
        _db = db;
        _completion = completion;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(EndMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        await _completion.CompleteAsync(match, "Ended by operator", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await _state.BuildAsync(match, cancellationToken);
    }
}
