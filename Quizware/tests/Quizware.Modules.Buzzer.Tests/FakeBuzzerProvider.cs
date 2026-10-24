using System.Collections.Concurrent;
using Quizware.Application.Buzzer;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>A stand-in for real hardware: available unless told otherwise,
/// remembers what it was asked to arm, returns scripted presses on collect,
/// and can be "unplugged" so every call throws as a dead serial port would.</summary>
public sealed class FakeBuzzerProvider : IBuzzerProvider
{
    public ConcurrentQueue<BuzzArmRequest> Armed { get; } = new();

    public List<BuzzPressResult> NextCollect { get; } = [];

    public bool Unplugged { get; set; }

    public string Name => "Fake";

    public bool IsAvailable { get; set; } = true;

    public int DeviceCount => 3;

    public Task<BuzzerHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BuzzerHealth(!Unplugged, Unplugged ? "Unplugged" : "OK",
            [.. Enumerable.Range(1, DeviceCount).Select(d => new BuzzerDeviceStatus(d, "FAKE", !Unplugged))]));

    public Task ArmAsync(BuzzArmRequest request, CancellationToken cancellationToken)
    {
        ThrowIfUnplugged();
        Armed.Enqueue(request);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        ThrowIfUnplugged();
        return Task.FromResult<IReadOnlyList<BuzzPressResult>>(NextCollect.ToList());
    }

    public Task ResetAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        ThrowIfUnplugged();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BuzzDeviceReading>> TestAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken)
    {
        ThrowIfUnplugged();
        return Task.FromResult<IReadOnlyList<BuzzDeviceReading>>(
            [.. deviceIds.Select(d => new BuzzDeviceReading(d, true, new Dictionary<string, int> { ["A"] = 100 * d }, null))]);
    }

    private void ThrowIfUnplugged()
    {
        if (Unplugged)
        {
            throw new IOException("The port is closed.");
        }
    }
}
