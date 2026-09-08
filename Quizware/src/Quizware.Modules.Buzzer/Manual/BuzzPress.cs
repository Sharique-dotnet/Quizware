namespace Quizware.Modules.Buzzer.Manual;

/// <summary>QuickBuzz currently stores nothing, so a disputed buzz cannot
/// be proved. Every press is persisted here with its raw frame.</summary>
public sealed class BuzzPress
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid BuzzSessionId { get; set; }
    public int DeviceId { get; set; }
    public Guid? TeamId { get; set; }
    public string ButtonKey { get; set; } = string.Empty;
    public int ElapsedMs { get; set; }
    public int Rank { get; set; }
    public string? RawFrameHex { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
