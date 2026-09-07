namespace QuizApp.Modules.Buzzer.Manual;

/// <summary>Solves QuickBuzz's missing device-to-team mapping — today a
/// human has to remember that device 2 is the team on the left.</summary>
public sealed class BuzzDeviceMapping
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? MatchId { get; set; }
    public int DeviceId { get; set; }
    public Guid? MatchParticipantId { get; set; }
    public Guid? TeamId { get; set; }
    public int? SeatNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
