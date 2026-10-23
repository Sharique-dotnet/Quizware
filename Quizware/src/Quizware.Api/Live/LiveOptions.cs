namespace Quizware.Api.Live;

/// <summary>"Live" settings for the push pipeline.</summary>
public sealed class LiveOptions
{
    public const string SectionName = "Live";

    /// <summary>Runs the outbox dispatcher and the timer ticks. Off only for test hosts that do not exercise them.</summary>
    public bool BackgroundServicesEnabled { get; set; } = true;

    /// <summary>How long the dispatcher sleeps without a signal before looking anyway (e.g. for retries).</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>First retry delay after a failed push; it doubles on each further failure.</summary>
    public int RetryBaseDelayMs { get; set; } = 500;

    /// <summary>Failed pushes after which a message is left undelivered (its Error kept for inspection).</summary>
    public int MaxRetries { get; set; } = 5;

    public int TimerTickIntervalMs { get; set; } = 1000;
}
