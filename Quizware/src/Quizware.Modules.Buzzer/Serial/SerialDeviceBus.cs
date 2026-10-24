using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Serial;

/// <summary>Ported from QuickBuzz's SerialService: owns the one serial port
/// and talks to each device in turn on the RS-485 bus. Changes from the
/// original: every constant comes from <see cref="SerialOptions"/>; logging
/// goes to ILogger; any number of devices; the start-byte search has a hard
/// deadline; I/O runs off the request thread; and one device that does not
/// answer is reported offline instead of failing the whole poll.</summary>
public sealed class SerialDeviceBus : IDisposable
{
    private readonly ISerialPortConnection _port;
    private readonly SerialOptions _options;
    private readonly ILogger<SerialDeviceBus> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SerialDeviceBus(ISerialPortConnection port, IOptions<BuzzerOptions> options, ILogger<SerialDeviceBus> logger)
    {
        _port = port;
        _options = options.Value.Serial;
        _logger = logger;
    }

    public string PortName => _port.PortName;

    /// <summary>Opens the port if needed; false (logged) if it cannot be opened.</summary>
    public bool TryOpen()
    {
        try
        {
            EnsureOpen();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex, "Serial port {Port} could not be opened", _port.PortName);
            return false;
        }
    }

    /// <summary>Sends <paramref name="command"/> to each device, with the bus settling delay between them.</summary>
    public Task SendAsync(IReadOnlyList<int> deviceIds, byte command, CancellationToken cancellationToken) =>
        WithPortAsync(async () =>
        {
            foreach (var deviceId in deviceIds)
            {
                _port.Write(DeviceParser.CommandFrame(deviceId, command));
                await Task.Delay(_options.BusDelayMs, cancellationToken);
            }

            return true;
        }, cancellationToken);

    /// <summary>Reads every device. A device that does not answer after the
    /// retries comes back as null (offline); a port that fails throws.</summary>
    public Task<IReadOnlyDictionary<int, DeviceTimes?>> PollAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) =>
        WithPortAsync<IReadOnlyDictionary<int, DeviceTimes?>>(async () =>
        {
            var readings = new Dictionary<int, DeviceTimes?>();
            foreach (var deviceId in deviceIds)
            {
                readings[deviceId] = ReadWithRetry(deviceId);
                await Task.Delay(_options.BusDelayMs, cancellationToken);
            }

            return readings;
        }, cancellationToken);

    public void Dispose()
    {
        _port.Dispose();
        _gate.Dispose();
    }

    private async Task<T> WithPortAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(
                async () =>
                {
                    EnsureOpen();
                    return await work();
                },
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureOpen()
    {
        if (!_port.IsOpen)
        {
            _port.Open();
            _logger.LogInformation("Serial port {Port} opened", _port.PortName);
        }
    }

    private DeviceTimes? ReadWithRetry(int deviceId)
    {
        for (var attempt = 1; attempt <= Math.Max(_options.RetryCount, 1); attempt++)
        {
            try
            {
                var reading = Read(deviceId);
                if (reading is not null)
                {
                    return reading;
                }

                _logger.LogDebug("Device {DeviceId} sent an unreadable frame (attempt {Attempt})", deviceId, attempt);
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("Device {DeviceId} timed out (attempt {Attempt})", deviceId, attempt);
            }
        }

        _logger.LogWarning("Device {DeviceId} did not answer after {Retries} attempts", deviceId, _options.RetryCount);
        return null;
    }

    private DeviceTimes? Read(int deviceId)
    {
        _port.DiscardInBuffer();
        _port.Write(DeviceParser.CommandFrame(deviceId, _options.Commands.Read));

        var clock = Stopwatch.StartNew();
        while (_port.ReadByte() != DeviceParser.StartByte)
        {
            if (clock.ElapsedMilliseconds > _options.SyncDeadlineMs)
            {
                throw new TimeoutException($"No frame start from device {deviceId} within {_options.SyncDeadlineMs} ms.");
            }
        }

        var frame = new byte[_options.FrameLength];
        frame[0] = DeviceParser.StartByte;
        for (var i = 1; i < frame.Length; i++)
        {
            frame[i] = (byte)_port.ReadByte();
        }

        var reading = DeviceParser.Parse(frame);
        return reading?.DeviceId == deviceId ? reading : null;
    }
}
