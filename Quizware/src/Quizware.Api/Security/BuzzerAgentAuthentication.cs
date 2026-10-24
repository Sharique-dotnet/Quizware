using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Quizware.Api.Security;

/// <summary>Q-005: the on-premises buzzer agent authenticates with a shared
/// key (<c>Buzzer:Agent:ApiKey</c>) sent as <c>X-Buzzer-Agent-Key</c> — it is a
/// machine, not a user, so it carries no JWT. With no key configured, no agent
/// can authenticate. The key only opens the agent endpoints (heartbeat and
/// press ingestion); it is never a user role.</summary>
public sealed class BuzzerAgentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "BuzzerAgent";
    public const string HeaderName = "X-Buzzer-Agent-Key";
    public const string ClaimType = "buzzer_agent";
    public const string ConfigurationKey = "Buzzer:Agent:ApiKey";

    private readonly IConfiguration _configuration;

    public BuzzerAgentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var presented) || string.IsNullOrEmpty(presented))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = _configuration[ConfigurationKey];
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("No buzzer agent key is configured."));
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented.ToString()), Encoding.UTF8.GetBytes(expected)))
        {
            return Task.FromResult(AuthenticateResult.Fail("The buzzer agent key is not valid."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "buzzer-agent"), new Claim(ClaimType, "true")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
