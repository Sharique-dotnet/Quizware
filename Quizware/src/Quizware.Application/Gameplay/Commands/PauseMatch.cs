using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record PauseMatchCommand(Guid MatchId) : IRequest<LiveMatchStateDto>;

public sealed class PauseMatchCommandHandler : IRequestHandler<PauseMatchCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public PauseMatchCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(PauseMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        match.Pause();
        await _eventLog.AppendAsync(match, MatchEventTypes.MatchPaused, new { }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await _state.BuildAsync(match, cancellationToken);
    }
}
