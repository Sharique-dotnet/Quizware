using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Quizware.Api.Controllers.v1;
using Quizware.Api.Security;
using Quizware.Infrastructure.Identity;

namespace Quizware.Api.IntegrationTests;

/// <summary>P15-02: the fixes from the OWASP Top 10 review — account lockout,
/// sign-in rate limiting, security headers, and refusing to start on
/// placeholder secrets.</summary>
public class SecurityHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "P@ssw0rd123!";

    private readonly CustomWebApplicationFactory _factory;

    public SecurityHardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> CreateUserAsync(WebApplicationFactory<Program> factory)
    {
        var email = $"sec-{Guid.NewGuid():N}@quizapp.test";
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, FullName = "Security Test", IsActive = true, EmailConfirmed = true };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_AndTheLockoutLooksLikeAWrongPassword()
    {
        var email = await CreateUserAsync(_factory);
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, email, "wrong-password")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var locked = await LoginAsync(client, email, Password);
        var unknown = await LoginAsync(client, "nobody@quizapp.test", Password);

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the right password does not open a locked account");
        (await locked.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync(),
            "a locked account must not be told apart from a wrong address");
    }

    [Fact]
    public async Task ASuccessfulSignIn_ResetsTheFailureCount()
    {
        var email = await CreateUserAsync(_factory);
        var client = _factory.CreateClient();

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < 4; i++)
            {
                await LoginAsync(client, email, "wrong-password");
            }

            (await LoginAsync(client, email, Password)).StatusCode.Should().Be(HttpStatusCode.OK, $"round {round + 1}: four failures stay under the limit");
        }
    }

    /// <summary>NFR-S4: nothing is anonymous except sign-in and health — plus
    /// token refresh, whose refresh token is itself the credential.</summary>
    [Fact]
    public void OnlySignInRefreshAndHealth_AreAnonymous()
    {
        var anonymous = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null
                || e.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText!.TrimStart('/'))
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        anonymous.Should().BeEquivalentTo("api/v1/admin/health", "api/v1/auth/login", "api/v1/auth/refresh", "health/live", "health/ready");
    }

    [Fact]
    public async Task ApiResponses_CarryTheSecurityHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
    }

    [Fact]
    public async Task SignIn_IsRateLimitedPerClientAddress()
    {
        using var limited = new RateLimitedFactory();
        var email = await CreateUserAsync(limited);
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await LoginAsync(client, email, Password)).StatusCode);
        }

        var rejected = await LoginAsync(client, email, Password);
        statuses.Take(3).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        statuses[3].Should().Be(HttpStatusCode.TooManyRequests);
        (await MatchTestHarness.ErrorCodeAsync(rejected)).Should().Be("RATE_LIMITED");
    }

    [Fact]
    public void Production_RefusesToStartOnPlaceholderSecrets()
    {
        static IConfiguration Config(params (string Key, string? Value)[] values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value))).Build();

        var placeholders = Config(
            ("Jwt:SigningKey", "CHANGE_ME_use_a_secret_at_least_32_bytes_long"), ("Jwt:Issuer", "CHANGE_ME"),
            ("Jwt:Audience", "quizware"), ("ConnectionStrings:Default", "CHANGE_ME"), ("Buzzer:Agent:ApiKey", "short"));
        var good = Config(
            ("Jwt:SigningKey", new string('k', 48)), ("Jwt:Issuer", "quizware"), ("Jwt:Audience", "quizware"),
            ("ConnectionStrings:Default", "Server=venue;Database=Quizware"), ("Buzzer:Agent:ApiKey", ""));

        var production = () => StartupSecurityChecks.Enforce(placeholders, new Env(Environments.Production));
        var development = () => StartupSecurityChecks.Enforce(placeholders, new Env(Environments.Development));

        StartupSecurityChecks.Problems(placeholders).Should().HaveCount(4);
        StartupSecurityChecks.Problems(good).Should().BeEmpty();
        production.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:SigningKey*");
        development.Should().NotThrow("development keeps its convenience defaults");
    }

    private sealed class RateLimitedFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(AuthRateLimiting.ConfigurationKey, "3");
        }
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Quizware.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
