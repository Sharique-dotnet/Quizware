namespace QuizApp.Modules.Buzzer.Manual;

public enum BuzzSessionState
{
    Armed = 1,
    Collected = 2,
    Reset = 3,
    Failed = 4,
}

public sealed class BuzzSession
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid MatchId { get; set; }
    public Guid? MatchQuestionId { get; set; }
    public BuzzSessionState State { get; set; } = BuzzSessionState.Armed;
    public DateTime? ArmedAtUtc { get; set; }
    public DateTime? CollectedAtUtc { get; set; }
    public string Provider { get; set; } = "Manual";
    public int WindowMs { get; set; } = 30000;
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
