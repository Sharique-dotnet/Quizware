namespace Quizware.Application.Abstractions;

/// <summary>Makes time-dependent logic deterministic in tests — never call
/// DateTime.UtcNow directly from Application-layer code.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
