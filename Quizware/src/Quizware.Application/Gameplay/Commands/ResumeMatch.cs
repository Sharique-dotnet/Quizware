using MediatR;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Gameplay.Commands;

public sealed record ResumeMatchCommand(Guid MatchId) : IRequest<LiveMatchStateDto>;

public sealed class ResumeMatchCommandHandler : IRequestHandler<ResumeMatchCommand, LiveMatchStateDto>
{
    private readonly IAppDbContext _db;
    private readonly MatchEventLog _eventLog;
    private readonly LiveStateBuilder _state;

    public ResumeMatchCommandHandler(IAppDbContext db, MatchEventLog eventLog, LiveStateBuilder state)
    {
        _db = db;
        _eventLog = eventLog;
        _state = state;
    }

    public async Task<LiveMatchStateDto> Handle(ResumeMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await MatchSetup.LoadMatchAsync(_db, null, request.MatchId, cancellationToken);
        match.Resume();
        await _eventLog.AppendAsync(match, MatchEventTypes.MatchResumed, new { }, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await _state.BuildAsync(match, cancellationToken);
    }
}
