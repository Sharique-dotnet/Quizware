using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quizware.Application.Buzzer;
using Quizware.Domain.Common.Exceptions;
using Quizware.Modules.Buzzer.Abstractions;
using Quizware.Modules.Buzzer.Serial;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>P14-04: the QuickBuzz frame parser and serial bus, ported — and
/// the defects the QuickBuzz audit found, fixed.</summary>
public class SerialProtocolTests
{
    private static (SerialDeviceBus Bus, SerialBuzzerAdapter Adapter, FakeSerialPort Port) Build(int deviceCount = 3, int syncDeadlineMs = 3000)
    {
        var port = new FakeSerialPort();
        var options = Options.Create(new BuzzerOptions
        {
            Provider = BuzzerOptions.ProviderSerial,
            DeviceCount = deviceCount,
            Serial = new SerialOptions { BusDelayMs = 0, RetryCount = 2, SyncDeadlineMs = syncDeadlineMs },
        });
        var bus = new SerialDeviceBus(port, options, NullLogger<SerialDeviceBus>.Instance);
        return (bus, new SerialBuzzerAdapter(bus, options), port);
    }

    [Fact]
    public void Parse_ReadsTheFiveBigEndianTimers_AndKeepsTheFrameAsHex()
    {
        var frame = FakeSerialPort.Frame(2, 0, 70000, 0, 300, 0);

        var parsed = DeviceParser.Parse(frame)!;

        parsed.DeviceId.Should().Be(2);
        parsed.ButtonTimesMs.Should().Equal(new Dictionary<string, int> { ["A"] = 0, ["B"] = 70000, ["C"] = 0, ["D"] = 300, ["E"] = 0 });
        parsed.FirstPress(["A", "B", "C", "D", "E"]).Should().Be(("D", 300));
        parsed.RawFrameHex.Should().HaveLength(48).And.StartWith("0202").And.EndWith("FE");
    }

    [Fact]
    public void Parse_RejectsAWrongLength_StartByte_OrEndByte()
    {
        var good = FakeSerialPort.Frame(1, 10);
        var badStart = (byte[])good.Clone();
        badStart[0] = 0x03;
        var badEnd = (byte[])good.Clone();
        badEnd[^1] = 0x00;

        DeviceParser.Parse(good[..23]).Should().BeNull();
        DeviceParser.Parse(badStart).Should().BeNull();
        DeviceParser.Parse(badEnd).Should().BeNull();
        DeviceParser.Parse(good).Should().NotBeNull();
    }

    [Fact]
    public async Task Collect_TakesEachDevicesEarliestButton_ForAnyNumberOfDevices_AndSkipsSilentOnes()
    {
        var (_, adapter, port) = Build(deviceCount: 5);
        port.Devices[1] = [0, 0, 0, 0, 450];
        port.Devices[2] = [0, 0, 0, 0, 0];
        port.Devices[4] = [900, 0, 120, 0, 0];
        port.Devices[5] = [0, 0, 0, 0, 77];

        var presses = await adapter.CollectAsync(Guid.NewGuid(), [1, 2, 3, 4, 5], CancellationToken.None);

        presses.Select(p => (p.DeviceId, p.ButtonKey, p.ElapsedMs)).Should().BeEquivalentTo(new[] { (1, "E", 450), (4, "C", 120), (5, "E", 77) });
        presses.Should().OnlyContain(p => p.RawFrameHex!.Length == 48);
    }

    [Fact]
    public async Task Arm_ResetsThenStartsEveryDevice()
    {
        var (_, adapter, port) = Build();

        await adapter.ArmAsync(new BuzzArmRequest(Guid.NewGuid(), Guid.NewGuid(), [1, 2], 30000), CancellationToken.None);

        port.Sent.Should().Equal((1, (byte)0x52), (2, (byte)0x52), (1, (byte)0x53), (2, (byte)0x53));
    }

    [Fact]
    public async Task LineNoise_WithNoFrameStart_GivesUpAtTheDeadline_InsteadOfHanging()
    {
        var (bus, _, port) = Build(syncDeadlineMs: 50);
        port.Devices[1] = [10, 0, 0, 0, 0];
        port.NoiseOnly = true;
        var clock = Stopwatch.StartNew();

        var readings = await bus.PollAsync([1], CancellationToken.None);

        readings[1].Should().BeNull("the device is reported offline");
        clock.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task AnUnpluggedCable_IsAnIOException_ThatTheGuardTurnsInto503_NotA500()
    {
        var (_, adapter, port) = Build();
        port.Devices[1] = [10, 0, 0, 0, 0];
        port.Unplugged = true;

        var collect = () => BuzzerCalls.GuardAsync(() => adapter.CollectAsync(Guid.NewGuid(), [1], CancellationToken.None));
        var noDeviceAnswers = async () =>
        {
            port.Unplugged = false;
            port.Devices.Clear();
            await adapter.CollectAsync(Guid.NewGuid(), [1, 2], CancellationToken.None);
        };

        adapter.IsAvailable.Should().BeFalse("the port cannot be opened");
        (await collect.Should().ThrowAsync<BuzzerUnavailableException>()).WithInnerException<IOException>();
        await noDeviceAnswers.Should().ThrowAsync<BuzzerUnavailableException>();
    }

    [Fact]
    public async Task Health_ReportsEachDevice_AndTest_ReadsEveryButton()
    {
        var (_, adapter, port) = Build();
        port.Devices[1] = [5, 6, 7, 8, 9];
        port.Devices[3] = [0, 0, 0, 0, 0];

        var health = await adapter.CheckHealthAsync(CancellationToken.None);
        var test = await adapter.TestAsync([1, 2], CancellationToken.None);

        health.Healthy.Should().BeTrue();
        health.Devices.Select(d => (d.DeviceId, d.IsOnline)).Should().Equal((1, true), (2, false), (3, true));
        test[0].ButtonTimesMs.Should().Equal(new Dictionary<string, int> { ["A"] = 5, ["B"] = 6, ["C"] = 7, ["D"] = 8, ["E"] = 9 });
        test[1].IsOnline.Should().BeFalse();
    }
}
