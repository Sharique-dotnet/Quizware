using System.Net.Http.Json;
using System.Text.Json;
using Quizware.Application.Buzzer;

namespace Quizware.Modules.Buzzer.Agent;

/// <summary>The armed session as the API reports it to the agent.</summary>
public sealed record AgentArmedSession(Guid SessionId, IReadOnlyList<int> DeviceIds, int WindowMs, DateTime ArmedAtUtc);

public sealed record AgentHeartbeatReply(IReadOnlyList<AgentArmedSession> ArmedSessions, int PollIntervalMs);

/// <summary>The agent's two calls to the API — both outbound.</summary>
public interface IBuzzerAgentApi
{
    Task<AgentHeartbeatReply> HeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken);

    Task SubmitPressAsync(Guid sessionId, BuzzPressResult press, CancellationToken cancellationToken);
}

/// <summary>Calls the API over HTTP. The <see cref="HttpClient"/> carries the
/// base address and the <c>X-Buzzer-Agent-Key</c> header.</summary>
public sealed class HttpBuzzerAgentApi : IBuzzerAgentApi
{
    public const string AgentKeyHeader = "X-Buzzer-Agent-Key";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HttpBuzzerAgentApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<AgentHeartbeatReply> HeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("api/v1/buzzer/agent/heartbeat", heartbeat, Json, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentHeartbeatReply>(Json, cancellationToken)
            ?? new AgentHeartbeatReply([], 500);
    }

    public async Task SubmitPressAsync(Guid sessionId, BuzzPressResult press, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            $"api/v1/buzzer/sessions/{sessionId}/presses",
            new { press.DeviceId, ElapsedMs = (long)press.ElapsedMs, RawFrameHex = press.RawFrameHex ?? string.Empty, press.ButtonKey },
            Json,
            cancellationToken);

        // 409: the session closed before this press arrived — nothing to retry.
        if (response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            response.EnsureSuccessStatusCode();
        }
    }
}
