namespace QuizApp.Domain.Common;

/// <summary>Stamped by an infrastructure interceptor — the developer never sets these by hand.</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }
    string CreatedBy { get; }
    DateTime? UpdatedAtUtc { get; }
    string? UpdatedBy { get; }
}
