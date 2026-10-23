using Microsoft.Extensions.Options;
using Quizware.Application.Buzzer;
using Quizware.Domain.Common.Exceptions;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Agent;

/// <summary>P14-06, the recommended production setup: the BuzzerAgent on the
/// operator PC owns the COM port. It calls in with heartbeats (learning which
/// session is armed) and pushes presses to the ingestion endpoint, so the API
/// never connects to the PC — no port forwarding or firewall rule needed.
/// Available while heartbeats keep arriving.</summary>
public sealed class HttpAgentBuzzerAdapter : IBuzzerProvider
{
    private readonly AgentHeartbeatRegistry _registry;
    private readonly BuzzerOptions _options;

    public HttpAgentBuzzerAdapter(AgentHeartbeatRegistry registry, IOptions<BuzzerOptions> options)
    {
        _registry = registry;
        _options = options.Value;
    }

    public string Name => BuzzerOptions.ProviderHttpAgent;

    public bool IsAvailable => _registry.Current is not null;

    public int DeviceCount => _options.DeviceCount;

    public Task<BuzzerHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (_registry.Current is not { } heartbeat)
        {
            var seen = _registry.LastSeenUtc is { } at ? $"last seen {at:u}" : "never seen";
            return Task.FromResult(new BuzzerHealth(false, $"The buzzer agent is not connected ({seen}).", []));
        }

        var devices = heartbeat.Devices.Select(d => new BuzzerDeviceStatus(d.DeviceId, heartbeat.PortName, d.IsOnline)).ToList();
        var online = devices.Count(d => d.IsOnline);
        return Task.FromResult(new BuzzerHealth(
            online > 0, $"Agent '{heartbeat.AgentId}' connected; {online} of {devices.Count} devices answering.", devices));
    }

    /// <summary>Nothing to send: the agent sees the armed session in its next heartbeat reply.</summary>
    public Task ArmAsync(BuzzArmRequest request, CancellationToken cancellationToken)
    {
        RequireAgent();
        return Task.CompletedTask;
    }

    /// <summary>The agent has already pushed every press; collecting needs it to
    /// still be connected, or the session is marked Failed.</summary>
    public Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        RequireAgent();
        return Task.FromResult<IReadOnlyList<BuzzPressResult>>([]);
    }

    /// <summary>The agent resets its devices when the session stops being armed.</summary>
    public Task ResetAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The button timers the agent read most recently.</summary>
    public Task<IReadOnlyList<BuzzDeviceReading>> TestAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        var heartbeat = RequireAgent();
        return Task.FromResult<IReadOnlyList<BuzzDeviceReading>>(heartbeat.Devices
            .Select(d => new BuzzDeviceReading(d.DeviceId, d.IsOnline, d.ButtonTimesMs ?? new Dictionary<string, int>(), d.RawFrameHex))
            .ToList());
    }

    private AgentHeartbeat RequireAgent() =>
        _registry.Current ?? throw new BuzzerUnavailableException(
            $"The buzzer agent has not been heard from in the last {_options.Agent.StaleAfterSeconds} seconds.");
}
