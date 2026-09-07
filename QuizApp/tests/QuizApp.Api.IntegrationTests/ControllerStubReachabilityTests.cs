using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Controllers.v1;
using QuizApp.Infrastructure.Identity;

namespace QuizApp.Api.IntegrationTests;

/// <summary>
/// P5-04: every documented route must exist and be reachable, even before any
/// business logic exists behind it. Hitting one representative route per
/// controller with a real, logged-in, program-scoped token proves routing +
/// auth + ProgramScopeMiddleware wiring is correct and the action returns 501
/// (not 404/405/403) — a full enumeration of all ~150 routes is not asserted
/// here, as a deliberate Phase 5 scope decision (see Implementation-Plan.md).
/// </summary>
public class ControllerStubReachabilityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ControllerStubReachabilityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Route templates using {programId}/{stageId}/{matchId} placeholders,
    /// substituted with real ids at test time so they match the token's scope.</summary>
    public static IEnumerable<object[]> RouteTemplates =>
    [
        [HttpMethod.Get, "/api/v1/programs"],
        [HttpMethod.Get, "/api/v1/programs/{programId}"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/teams"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/topics"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/tags"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/questions"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/questions/coverage"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/stages"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/rules/scoring"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/matches"],
        [HttpMethod.Get, "/api/v1/matches/{matchId}/live/state"],
        [HttpMethod.Get, "/api/v1/matches/{matchId}/scores"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/standings/overall"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/qualification/stages/{stageId}/preview"],
        [HttpMethod.Get, "/api/v1/buzzer/capability"],
        [HttpMethod.Get, "/api/v1/programs/{programId}/reports/questions/usage"],
        [HttpMethod.Get, "/api/v1/admin/lookups"],
        [HttpMethod.Get, "/api/v1/auth/me"],
    ];

    private async Task<string> LoginAsAsync(HttpClient client, string email, string password, Guid? programId, params string[] roles)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, password);
            created.Succeeded.Should().BeTrue(string.Join(", ", created.Errors.Select(e => e.Description)));
            if (roles.Length > 0)
            {
                await userManager.AddToRolesAsync(user, roles);
            }
        }

        if (programId is null)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
            return body!.AccessToken;
        }

        // No real "select program" flow exists yet (it is itself a Phase 5
        // stub) — mint a program-scoped token directly via IJwtTokenService,
        // exactly as AuthController will once P6-09 implements it for real.
        using var tokenScope = _factory.Services.CreateScope();
        var userManagerForToken = tokenScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokenService = tokenScope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var storedUser = await userManagerForToken.FindByEmailAsync(email);
        var storedRoles = await userManagerForToken.GetRolesAsync(storedUser!);
        return tokenService.GenerateAccessToken(storedUser!, storedRoles, programId);
    }

    [Theory]
    [MemberData(nameof(RouteTemplates))]
    public async Task Route_AsSuperAdmin_Returns501NotImplemented(HttpMethod method, string routeTemplate)
    {
        var programId = Guid.NewGuid();
        var path = routeTemplate
            .Replace("{programId}", programId.ToString())
            .Replace("{stageId}", Guid.NewGuid().ToString())
            .Replace("{matchId}", Guid.NewGuid().ToString());

        var client = _factory.CreateClient();
        var needsProgramScope = routeTemplate.Contains("{programId}");
        var token = await LoginAsAsync(
            client,
            $"superadmin-{Guid.NewGuid():N}@quizapp.test",
            "P@ssw0rd123!",
            needsProgramScope ? programId : null,
            "SuperAdmin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(new HttpRequestMessage(method, path));

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented, $"{method} {path} should be routed to a stub action");
    }

    [Fact]
    public async Task DisplayEndpoint_WithoutDisplayRole_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAsync(client, $"operator-{Guid.NewGuid():N}@quizapp.test", "P@ssw0rd123!", null, "Operator");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/v1/display/programs/{Guid.NewGuid()}/branding");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProgramCreate_WithoutSuperAdminRole_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAsync(client, $"progadmin-{Guid.NewGuid():N}@quizapp.test", "P@ssw0rd123!", null, "ProgramAdmin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/programs", new { name = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProgramScopedRoute_WithMismatchedTokenProgramId_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAsync(client, $"scoped-{Guid.NewGuid():N}@quizapp.test", "P@ssw0rd123!", Guid.NewGuid(), "SuperAdmin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/v1/programs/{Guid.NewGuid()}/teams");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "ADR-002: {programId} must match the token's program_id claim exactly");
    }

    [Fact]
    public async Task AnyRoute_WithoutToken_IsUnauthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/programs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
