using Microsoft.Extensions.Options;
using Quizware.Application.Buzzer;
using Quizware.Domain.Common.Exceptions;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Serial;

/// <summary>P14-04: the API itself owns the RS-485 port (when the API runs on
/// the machine with the cable). Arming resets and starts the devices;
/// collecting polls them and takes each device's earliest press.</summary>
public sealed class SerialBuzzerAdapter : IBuzzerProvider
{
    private readonly SerialDeviceBus _bus;
    private readonly BuzzerOptions _options;

    public SerialBuzzerAdapter(SerialDeviceBus bus, IOptions<BuzzerOptions> options)
    {
        _bus = bus;
        _options = options.Value;
    }

    public string Name => BuzzerOptions.ProviderSerial;

    /// <summary>True while the port can be opened; a fault later in a call is
    /// still a 503 for that call.</summary>
    public bool IsAvailable => _bus.TryOpen();

    public int DeviceCount => _options.DeviceCount;

    public async Task<BuzzerHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (!_bus.TryOpen())
        {
            return new BuzzerHealth(false, $"Serial port {_bus.PortName} cannot be opened.", []);
        }

        var readings = await _bus.PollAsync(AllDevices(), cancellationToken);
        var devices = readings.Select(r => new BuzzerDeviceStatus(r.Key, _bus.PortName, r.Value is not null)).ToList();
        var online = devices.Count(d => d.IsOnline);
        return new BuzzerHealth(online > 0, $"{online} of {devices.Count} devices answered on {_bus.PortName}.", devices);
    }

    public async Task ArmAsync(BuzzArmRequest request, CancellationToken cancellationToken)
    {
        await _bus.SendAsync(request.DeviceIds, _options.Serial.Commands.Reset, cancellationToken);
        await _bus.SendAsync(request.DeviceIds, _options.Serial.Commands.Arm, cancellationToken);
    }

    public async Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        var readings = await _bus.PollAsync(deviceIds, cancellationToken);
        if (readings.Count > 0 && readings.Values.All(r => r is null))
        {
            throw new BuzzerUnavailableException($"No buzzer device answered on {_bus.PortName}.");
        }

        return readings.Values
            .Where(r => r is not null)
            .Select(r => (Reading: r!, Press: r!.FirstPress(_options.Serial.Buttons)))
            .Where(x => x.Press is not null)
            .Select(x => new BuzzPressResult(x.Reading.DeviceId, x.Press!.Value.Button, x.Press.Value.ElapsedMs, x.Reading.RawFrameHex))
            .ToList();
    }

    public Task ResetAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) =>
        _bus.SendAsync(deviceIds, _options.Serial.Commands.Reset, cancellationToken);

    public async Task<IReadOnlyList<BuzzDeviceReading>> TestAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        var readings = await _bus.PollAsync(deviceIds, cancellationToken);
        return readings
            .Select(r => new BuzzDeviceReading(
                r.Key, r.Value is not null, r.Value?.ButtonTimesMs ?? new Dictionary<string, int>(), r.Value?.RawFrameHex))
            .ToList();
    }

    private List<int> AllDevices() => Enumerable.Range(1, Math.Max(_options.DeviceCount, 1)).ToList();
}
