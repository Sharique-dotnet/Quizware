using System.Text;

namespace Quizware.Api.Security;

/// <summary>P15-02 (OWASP A05/A02): outside Development and Testing, the API
/// refuses to start with the placeholder secrets from appsettings.json or a
/// signing key too short to be safe — a misconfigured venue server fails
/// loudly at start-up instead of running with a guessable key.</summary>
public static class StartupSecurityChecks
{
    public const int MinimumSigningKeyBytes = 32;
    public const int MinimumAgentKeyLength = 16;

    public static IReadOnlyList<string> Problems(IConfiguration configuration)
    {
        var problems = new List<string>();
        var signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        if (signingKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || Encoding.UTF8.GetByteCount(signingKey) < MinimumSigningKeyBytes)
        {
            problems.Add($"Jwt:SigningKey must be set to a secret of at least {MinimumSigningKeyBytes} bytes (it is a placeholder or too short).");
        }

        foreach (var key in new[] { "Jwt:Issuer", "Jwt:Audience", "ConnectionStrings:Default" })
        {
            if (string.IsNullOrWhiteSpace(configuration[key]) || configuration[key]!.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{key} must be configured (it is empty or a placeholder).");
            }
        }

        var agentKey = configuration["Buzzer:Agent:ApiKey"];
        if (!string.IsNullOrEmpty(agentKey) && agentKey.Length < MinimumAgentKeyLength)
        {
            problems.Add($"Buzzer:Agent:ApiKey must be at least {MinimumAgentKeyLength} characters when set.");
        }

        return problems;
    }

    public static void Enforce(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return;
        }

        var problems = Problems(configuration);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Refusing to start with an insecure configuration:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }
    }
}
