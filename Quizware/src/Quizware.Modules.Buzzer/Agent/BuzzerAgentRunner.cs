using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quizware.Application.Buzzer;
using Quizware.Modules.Buzzer.Abstractions;
using Quizware.Modules.Buzzer.Serial;

namespace Quizware.Modules.Buzzer.Agent;

/// <summary>P14-05: the BuzzerAgent's loop, one <see cref="TickAsync"/> per
/// interval. Each tick: heartbeat (reporting the devices) → follow the armed
/// session the API names (arming the devices for a new one, resetting them
/// when it ends) → poll the devices → push every new first press. A press
/// that fails to send is retried on the next tick; a serial fault is
/// reported as devices offline, never a crash.</summary>
public sealed class BuzzerAgentRunner
{
    private readonly SerialDeviceBus _bus;
    private readonly IBuzzerAgentApi _api;
    private readonly BuzzerOptions _options;
    private readonly ILogger<BuzzerAgentRunner> _logger;
    private readonly HashSet<int> _pushed = [];
    private IReadOnlyDictionary<int, DeviceTimes?> _lastReadings = new Dictionary<int, DeviceTimes?>();

    public BuzzerAgentRunner(SerialDeviceBus bus, IBuzzerAgentApi api, IOptions<BuzzerOptions> options, ILogger<BuzzerAgentRunner> logger)
    {
        _bus = bus;
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public string AgentId { get; set; } = Environment.MachineName;

    public AgentArmedSession? CurrentSession { get; private set; }

    /// <summary>Runs one cycle and returns how long to wait before the next.</summary>
    public async Task<TimeSpan> TickAsync(CancellationToken cancellationToken)
    {
        var reply = await _api.HeartbeatAsync(
            new AgentHeartbeat(AgentId, _bus.PortName, _lastReadings
                .Select(r => new AgentDeviceReport(r.Key, r.Value is not null, r.Value?.ButtonTimesMs, r.Value?.RawFrameHex))
                .ToList()),
            cancellationToken);

        await FollowAsync(reply.ArmedSessions.OrderByDescending(s => s.ArmedAtUtc).FirstOrDefault(), cancellationToken);

        var devices = CurrentSession?.DeviceIds ?? Enumerable.Range(1, Math.Max(_options.DeviceCount, 1)).ToList();
        try
        {
            _lastReadings = await _bus.PollAsync(devices, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Polling the buzzer devices failed");
            _lastReadings = devices.ToDictionary(d => d, _ => (DeviceTimes?)null);
        }

        if (CurrentSession is { } session)
        {
            await PushNewPressesAsync(session, cancellationToken);
        }

        return TimeSpan.FromMilliseconds(Math.Max(reply.PollIntervalMs, 50));
    }

    private async Task FollowAsync(AgentArmedSession? armed, CancellationToken cancellationToken)
    {
        if (armed?.SessionId == CurrentSession?.SessionId)
        {
            return;
        }

        if (CurrentSession is { } ended)
        {
            await SendSafelyAsync(ended.DeviceIds, _options.Serial.Commands.Reset, cancellationToken);
            _logger.LogInformation("Buzz session {SessionId} ended; devices reset", ended.SessionId);
        }

        CurrentSession = armed;
        _pushed.Clear();
        if (armed is not null)
        {
            await SendSafelyAsync(armed.DeviceIds, _options.Serial.Commands.Reset, cancellationToken);
            await SendSafelyAsync(armed.DeviceIds, _options.Serial.Commands.Arm, cancellationToken);
            _logger.LogInformation("Buzz session {SessionId} armed on devices {Devices}", armed.SessionId, string.Join(",", armed.DeviceIds));
        }
    }

    private async Task PushNewPressesAsync(AgentArmedSession session, CancellationToken cancellationToken)
    {
        foreach (var (deviceId, reading) in _lastReadings)
        {
            if (reading is null || _pushed.Contains(deviceId) || reading.FirstPress(_options.Serial.Buttons) is not { } press)
            {
                continue;
            }

            try
            {
                await _api.SubmitPressAsync(session.SessionId, new BuzzPressResult(deviceId, press.Button, press.ElapsedMs, reading.RawFrameHex), cancellationToken);
                _pushed.Add(deviceId);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Pushing device {DeviceId}'s press failed; retrying next tick", deviceId);
            }
        }
    }

    private async Task SendSafelyAsync(IReadOnlyList<int> devices, byte command, CancellationToken cancellationToken)
    {
        try
        {
            await _bus.SendAsync(devices, command, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Sending command 0x{Command:X2} to the buzzer devices failed", command);
        }
    }
}
