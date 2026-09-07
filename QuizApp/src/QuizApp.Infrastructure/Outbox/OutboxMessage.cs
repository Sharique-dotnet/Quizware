namespace QuizApp.Infrastructure.Outbox;

/// <summary>Guarantees SignalR notifications are sent even if the push
/// fails at the moment of the database commit (ADR-006).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
