namespace Quizware.Domain.Common.Exceptions;

/// <summary>No buzzer provider is configured, the hardware or agent is not
/// responding, or the buzzer module is not installed — 503
/// <c>BUZZER_UNAVAILABLE</c>. Never blocks gameplay: answers are recorded the
/// same way without a buzzer (ADR-005).</summary>
public sealed class BuzzerUnavailableException : Exception
{
    public BuzzerUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
