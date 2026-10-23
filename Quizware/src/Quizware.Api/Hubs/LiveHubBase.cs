using MediatR;
using Microsoft.AspNetCore.SignalR;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Application.Gameplay;
using Quizware.Application.Gameplay.Queries;

namespace Quizware.Api.Hubs;

/// <summary>What both hubs share (05-API-Design.md §5.7): a client joins a
/// match's group, gets its full state straight back, and can ask for it again
/// after a reconnect (P12-06). Nothing here writes — live actions stay on the
/// REST API, which is where their authorisation lives. The tenant filter
/// applies as on any request, so a match outside the caller's program is not found.</summary>
public abstract class LiveHubBase : Hub
{
    private const string JoinedMatchKey = "matchId";

    private readonly ISender _sender;

    protected LiveHubBase(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Whose view of the state this hub serves; null means the caller's own role decides.</summary>
    protected abstract LiveAudience? Audience { get; }

    public static string GroupFor(Guid matchId) => $"match-{matchId}";

    public async Task<LiveMatchStateResponse> JoinMatch(Guid matchId)
    {
        var state = await StateAsync(matchId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(matchId));
        Context.Items[JoinedMatchKey] = matchId;
        return state;
    }

    public async Task LeaveMatch(Guid matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(matchId));
        if (Context.Items.TryGetValue(JoinedMatchKey, out var joined) && joined is Guid id && id == matchId)
        {
            Context.Items.Remove(JoinedMatchKey);
        }
    }

    /// <summary>The full state of the match this connection last joined — the
    /// re-sync a client asks for after reconnecting, instead of trusting it saw every event.</summary>
    public Task<LiveMatchStateResponse> RequestFullState() =>
        Context.Items.TryGetValue(JoinedMatchKey, out var joined) && joined is Guid matchId
            ? StateAsync(matchId)
            : throw new HubException("Join a match first.");

    private async Task<LiveMatchStateResponse> StateAsync(Guid matchId)
    {
        try
        {
            return LiveMapper.ToResponse(await _sender.Send(new GetLiveMatchStateQuery(matchId, Audience), Context.ConnectionAborted));
        }
        catch (KeyNotFoundException)
        {
            throw new HubException($"Match '{matchId}' was not found.");
        }
    }
}
