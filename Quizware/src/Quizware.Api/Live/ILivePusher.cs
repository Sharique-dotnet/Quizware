using System.Text.Json;

namespace Quizware.Api.Live;

/// <summary>One outbox message, ready to push.</summary>
public sealed record LiveDispatch(Guid OutboxMessageId, string Type, Guid ProgramId, Guid MatchId, DateTime OccurredAtUtc, JsonElement Data);

/// <summary>Delivers one live event to the hubs. Throwing means "not
/// delivered": the dispatcher keeps the message and retries it.</summary>
public interface ILivePusher
{
    Task PushAsync(LiveDispatch dispatch, CancellationToken cancellationToken);
}
