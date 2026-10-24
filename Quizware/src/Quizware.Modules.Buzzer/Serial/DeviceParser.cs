using System.Buffers.Binary;

namespace Quizware.Modules.Buzzer.Serial;

/// <summary>One device's reply: the timer of every button in milliseconds
/// since the device was armed (0 = not pressed), and the frame as hex.</summary>
public sealed record DeviceTimes(int DeviceId, byte Command, IReadOnlyDictionary<string, int> ButtonTimesMs, string RawFrameHex)
{
    /// <summary>The earliest press among <paramref name="buttons"/>, if any.</summary>
    public (string Button, int ElapsedMs)? FirstPress(IEnumerable<string> buttons) =>
        buttons
            .Where(b => ButtonTimesMs.TryGetValue(b, out var ms) && ms > 0)
            .Select(b => (Button: b, ElapsedMs: ButtonTimesMs[b]))
            .OrderBy(p => p.ElapsedMs)
            .Cast<(string, int)?>()
            .FirstOrDefault();
}

/// <summary>Ported from QuickBuzz's DeviceParser. The 24-byte frame:
/// <c>0x02</c> start, device id, command byte, five big-endian 32-bit
/// millisecond timers for buttons A–E, and a <c>0xFD</c>/<c>0xFE</c> end byte.
/// Returns null for anything else (QuickBuzz returned null while declaring a
/// non-nullable result — audit defect #5).</summary>
public static class DeviceParser
{
    public const byte StartByte = 0x02;
    public const int FrameLength = 24;
    public static readonly string[] ButtonKeys = ["A", "B", "C", "D", "E"];

    private const int TimersOffset = 3;

    public static DeviceTimes? Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != FrameLength || frame[0] != StartByte || frame[^1] is not (0xFD or 0xFE))
        {
            return null;
        }

        var times = new Dictionary<string, int>(ButtonKeys.Length);
        for (var i = 0; i < ButtonKeys.Length; i++)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(TimersOffset + (i * 4), 4));
            times[ButtonKeys[i]] = value > int.MaxValue ? int.MaxValue : (int)value;
        }

        return new DeviceTimes(frame[1], frame[2], times, Convert.ToHexString(frame));
    }

    /// <summary>A command frame for one device — the same 24-byte shape, timers zero.</summary>
    public static byte[] CommandFrame(int deviceId, byte command)
    {
        var frame = new byte[FrameLength];
        frame[0] = StartByte;
        frame[1] = checked((byte)deviceId);
        frame[2] = command;
        frame[^1] = 0xFD;
        return frame;
    }
}
