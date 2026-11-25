using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Gameplay;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.Live;

/// <summary>Pushes an event to the match's group on both hubs, each with a
/// state snapshot built for its own audience at dispatch time: the console
/// view on <c>/hubs/match</c>, the display view (answer withheld until
/// revealed, event details sanitised) on <c>/hubs/display</c>. The pusher
/// lives for one dispatch pass, and a match's snapshots are built once per
/// pass (P15-06): every event in the pass was committed before it began, so
/// the first snapshot already includes them all.</summary>
public sealed class SignalRLivePusher : ILivePusher
{
    private readonly AppDbContext _db;
    private readonly LiveStateBuilder _state;
    private readonly IHubContext<MatchHub> _matchHub;
    private readonly IHubContext<DisplayHub> _displayHub;
    private readonly Dictionary<Guid, (LiveMatchStateResponse? Console, LiveMatchStateResponse? Display)> _snapshots = [];

    public SignalRLivePusher(AppDbContext db, LiveStateBuilder state, IHubContext<MatchHub> matchHub, IHubContext<DisplayHub> displayHub)
    {
        _db = db;
        _state = state;
        _matchHub = matchHub;
        _displayHub = displayHub;
    }

    public async Task PushAsync(LiveDispatch dispatch, CancellationToken cancellationToken)
    {
        if (!_snapshots.TryGetValue(dispatch.MatchId, out var snapshot))
        {
            var match = await _db.Matches.SingleOrDefaultAsync(m => m.Id == dispatch.MatchId, cancellationToken);
            snapshot = match is null
                ? (null, null)
                : (LiveMapper.ToResponse(await _state.BuildAsync(match, LiveAudience.Console, cancellationToken)),
                   LiveMapper.ToResponse(await _state.BuildAsync(match, LiveAudience.Display, cancellationToken)));
            _snapshots[dispatch.MatchId] = snapshot;
        }

        var (consoleState, displayState) = snapshot;

        var group = LiveHubBase.GroupFor(dispatch.MatchId);
        await _matchHub.Clients.Group(group).SendAsync(
            dispatch.Type,
            new LiveEventMessage(dispatch.Type, dispatch.MatchId, dispatch.OccurredAtUtc, dispatch.Data, consoleState),
            cancellationToken);
        await _displayHub.Clients.Group(group).SendAsync(
            dispatch.Type,
            new LiveEventMessage(dispatch.Type, dispatch.MatchId, dispatch.OccurredAtUtc, LiveEventSanitizer.ForDisplay(dispatch.Type, dispatch.Data), displayState),
            cancellationToken);
    }
}
