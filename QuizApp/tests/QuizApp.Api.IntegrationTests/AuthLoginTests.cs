using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Controllers.v1;
using QuizApp.Infrastructure.Identity;

namespace QuizApp.Api.IntegrationTests;

public class AuthLoginTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthLoginTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task CreateUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUsableJwt()
    {
        await CreateUserAsync("owner@quizapp.test", "P@ssw0rd123!");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("owner@quizapp.test", "P@ssw0rd123!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.AccessToken.Split('.').Should().HaveCount(3, "a JWT has three dot-separated segments");
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthenticated()
    {
        await CreateUserAsync("wrongpass@quizapp.test", "P@ssw0rd123!");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("wrongpass@quizapp.test", "not-the-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthenticated_NotNotFound()
    {
        // Never reveal whether the email exists — same response either way.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("nobody@quizapp.test", "whatever"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
