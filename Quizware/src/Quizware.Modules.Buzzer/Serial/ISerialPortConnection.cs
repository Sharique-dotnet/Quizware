using System.IO.Ports;
using Quizware.Modules.Buzzer.Abstractions;

namespace Quizware.Modules.Buzzer.Serial;

/// <summary>The serial port, behind an interface so the bus logic can be
/// tested without hardware.</summary>
public interface ISerialPortConnection : IDisposable
{
    string PortName { get; }

    bool IsOpen { get; }

    void Open();

    void Write(byte[] buffer);

    /// <summary>One byte, or <see cref="TimeoutException"/> after the read timeout.</summary>
    int ReadByte();

    void DiscardInBuffer();
}

public sealed class SystemSerialPortConnection : ISerialPortConnection
{
    private readonly SerialPort _port;

    public SystemSerialPortConnection(SerialOptions options)
    {
        _port = new SerialPort(options.PortName, options.BaudRate)
        {
            ReadTimeout = options.ReadTimeoutMs,
            WriteTimeout = options.ReadTimeoutMs,
        };
    }

    public string PortName => _port.PortName;

    public bool IsOpen => _port.IsOpen;

    public void Open() => _port.Open();

    public void Write(byte[] buffer) => _port.Write(buffer, 0, buffer.Length);

    public int ReadByte() => _port.ReadByte();

    public void DiscardInBuffer() => _port.DiscardInBuffer();

    public void Dispose() => _port.Dispose();
}
