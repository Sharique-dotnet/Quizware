using System.Buffers.Binary;
using System.Collections.Concurrent;
using Quizware.Modules.Buzzer.Serial;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>Simulates the RS-485 rig: devices answer a read command with a
/// 24-byte frame of their button timers. It can go silent per device, send
/// line noise with no frame start (QuickBuzz's infinite-loop case), or be
/// unplugged so every write throws, as a pulled cable does.</summary>
public sealed class FakeSerialPort : ISerialPortConnection
{
    private readonly ConcurrentQueue<byte> _incoming = new();

    public byte ReadCommand { get; set; } = 0x47;

    public byte ResetCommand { get; set; } = 0x52;

    /// <summary>Device id → button timers A–E (ms, 0 = not pressed). Missing = silent.</summary>
    public ConcurrentDictionary<int, int[]> Devices { get; } = new();

    public List<(int DeviceId, byte Command)> Sent { get; } = [];

    public bool Unplugged { get; set; }

    public bool NoiseOnly { get; set; }

    public string PortName => "FAKE1";

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (Unplugged)
        {
            throw new IOException("The port 'FAKE1' does not exist.");
        }

        IsOpen = true;
    }

    public void Write(byte[] buffer)
    {
        if (Unplugged)
        {
            IsOpen = false;
            throw new IOException("The device is not connected.");
        }

        int device = buffer[1];
        var command = buffer[2];
        lock (Sent)
        {
            Sent.Add((device, command));
        }

        if (command == ResetCommand && Devices.TryGetValue(device, out var times))
        {
            Array.Clear(times);
        }

        if (command == ReadCommand && Devices.TryGetValue(device, out var timers))
        {
            _incoming.Enqueue(0x55);
            foreach (var b in Frame(device, timers))
            {
                _incoming.Enqueue(b);
            }
        }
    }

    public int ReadByte()
    {
        if (NoiseOnly)
        {
            return 0x55;
        }

        return _incoming.TryDequeue(out var b) ? b : throw new TimeoutException("The operation has timed out.");
    }

    public void DiscardInBuffer() => _incoming.Clear();

    public void Dispose()
    {
    }

    public static byte[] Frame(int deviceId, params int[] timers)
    {
        var frame = new byte[DeviceParser.FrameLength];
        frame[0] = DeviceParser.StartByte;
        frame[1] = (byte)deviceId;
        frame[2] = 0x47;
        for (var i = 0; i < 5; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(3 + (i * 4), 4), (uint)(i < timers.Length ? timers[i] : 0));
        }

        frame[^1] = 0xFE;
        return frame;
    }
}
