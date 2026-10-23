namespace Quizware.Modules.Buzzer.Abstractions;

/// <summary>The "Buzzer" configuration section (02-Architecture-Proposal.md
/// §2.14). Every value QuickBuzz hardcoded lives here.</summary>
public sealed class BuzzerOptions
{
    public const string SectionName = "Buzzer";

    public const string ProviderNone = "None";
    public const string ProviderSerial = "Serial";
    public const string ProviderHttpAgent = "HttpAgent";

    /// <summary>None | Serial | HttpAgent.</summary>
    public string Provider { get; set; } = ProviderNone;

    /// <summary>How many devices to arm and poll when a match has no device
    /// mappings — a default, not a limit (D-014).</summary>
    public int DeviceCount { get; set; } = 3;

    /// <summary>How long a session stays open for presses.</summary>
    public int WindowMs { get; set; } = 30_000;

    public SerialOptions Serial { get; set; } = new();

    public AgentOptions Agent { get; set; } = new();
}

/// <summary>The HTTP-agent provider. <see cref="ApiKey"/> is read by the API's
/// agent authentication (Q-005); empty means no agent can connect.</summary>
public sealed class AgentOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The agent counts as gone after this long without a heartbeat.</summary>
    public int StaleAfterSeconds { get; set; } = 10;

    /// <summary>How often the agent is told to call back while idle or armed.</summary>
    public int PollIntervalMs { get; set; } = 250;
}

/// <summary>The RS-485 bus settings QuickBuzz hardcoded in SerialService.
/// The defaults are QuickBuzz's values; <see cref="Commands"/> are not
/// recorded in the QuickBuzz audit and must be checked against its
/// SerialService before real hardware is connected.</summary>
public sealed class SerialOptions
{
    public string PortName { get; set; } = "COM3";

    public int BaudRate { get; set; } = 38400;

    public int FrameLength { get; set; } = 24;

    /// <summary>Per-byte read timeout.</summary>
    public int ReadTimeoutMs { get; set; } = 1500;

    public int RetryCount { get; set; } = 3;

    /// <summary>Settling delay between devices on the shared bus (tuned on real hardware).</summary>
    public int BusDelayMs { get; set; } = 150;

    /// <summary>Hard limit on finding a frame's start byte. QuickBuzz had none,
    /// so line noise could hang a poll forever (audit defect #2).</summary>
    public int SyncDeadlineMs { get; set; } = 3000;

    /// <summary>The buttons whose timers count as a press, earliest wins.</summary>
    public List<string> Buttons { get; set; } = ["A", "B", "C", "D", "E"];

    public SerialCommands Commands { get; set; } = new();
}

/// <summary>Command bytes sent to a device. [ASSUMED] defaults — confirm
/// against QuickBuzz's SerialService (start/reset/read) before go-live.</summary>
public sealed class SerialCommands
{
    public byte Read { get; set; } = 0x47;

    public byte Arm { get; set; } = 0x53;

    public byte Reset { get; set; } = 0x52;
}
