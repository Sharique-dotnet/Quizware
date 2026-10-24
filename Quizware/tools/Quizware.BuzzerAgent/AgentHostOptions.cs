namespace Quizware.BuzzerAgent;

/// <summary>The "Agent" configuration section: where the API is and how to authenticate.</summary>
public sealed class AgentHostOptions
{
    public const string SectionName = "Agent";

    /// <summary>The venue server, e.g. https://quiz-server:5001/.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5299/";

    /// <summary>Must match the API's <c>Buzzer:Agent:ApiKey</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string AgentId { get; set; } = Environment.MachineName;

    public int TimeoutMs { get; set; } = 3000;

    /// <summary>The wait before trying again when the API cannot be reached.</summary>
    public int RetryDelayMs { get; set; } = 2000;
}
