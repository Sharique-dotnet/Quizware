using Microsoft.Extensions.Options;
using Quizware.Application.Abstractions;
using Quizware.Application.Buzzer;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Agent;

/// <summary>The last heartbeat from the buzzer agent, held in memory — the API
/// runs as one process on the venue server (ADR-008), and a restart only
/// costs the agent one heartbeat interval to be seen again.</summary>
public sealed class AgentHeartbeatRegistry : IBuzzerAgentLink
{
    private readonly IClock _clock;
    private readonly AgentOptions _options;
    private readonly object _gate = new();
    private (AgentHeartbeat Heartbeat, DateTime AtUtc)? _last;

    public AgentHeartbeatRegistry(IClock clock, IOptions<BuzzerOptions> options)
    {
        _clock = clock;
        _options = options.Value.Agent;
    }

    public bool AcceptsAgent => true;

    public int PollIntervalMs => _options.PollIntervalMs;

    public void RecordHeartbeat(AgentHeartbeat heartbeat)
    {
        lock (_gate)
        {
            _last = (heartbeat, _clock.UtcNow);
        }
    }

    /// <summary>The latest heartbeat if it is recent enough, otherwise null.</summary>
    public AgentHeartbeat? Current
    {
        get
        {
            lock (_gate)
            {
                return _last is { } last && _clock.UtcNow - last.AtUtc <= TimeSpan.FromSeconds(_options.StaleAfterSeconds) ? last.Heartbeat : null;
            }
        }
    }

    public DateTime? LastSeenUtc
    {
        get
        {
            lock (_gate)
            {
                return _last?.AtUtc;
            }
        }
    }
}
