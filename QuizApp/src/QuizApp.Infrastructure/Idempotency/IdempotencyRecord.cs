namespace QuizApp.Infrastructure.Idempotency;

/// <summary>Same key + same body replays the stored response; same key +
/// different body is a 409 IDEMPOTENCY_MISMATCH.</summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestBodyHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
