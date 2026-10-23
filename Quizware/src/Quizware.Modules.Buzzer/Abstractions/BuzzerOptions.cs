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
}
