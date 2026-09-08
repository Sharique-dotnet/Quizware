using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Contracts.V1.Admin;
using QuizApp.Api.Contracts.V1.Auth;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Api.Controllers.v1;
using QuizApp.Application.Authorization;
using QuizApp.Infrastructure.Identity;

namespace QuizApp.Api.IntegrationTests;

/// <summary>Phase 6b: AdminController's user-management actions are no
/// longer 501 stubs, plus the brand-new reset-password route.</summary>
public class AdminUsersEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminUsersEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientAsAsync(params string[] roles)
    {
        var email = $"admin-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

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

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<Guid> CreateProgramAsync(HttpClient superAdminClient)
    {
        var response = await superAdminClient.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest($"P-{Guid.NewGuid():N}", "Test Program", null));
        var program = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        return program!.Id;
    }

    [Fact]
    public async Task ListUsers_ReturnsInvitedUser()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var email = $"listed-{Guid.NewGuid():N}@quizapp.test";
        await admin.PostAsJsonAsync("/api/v1/admin/users", new InviteUserRequest(email, "Listed User", [Roles.Operator]));

        var response = await admin.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<AdminUsersResponse>();
        users!.Users.Should().Contain(u => u.Email == email && u.Roles.Contains(Roles.Operator));
    }

    [Fact]
    public async Task InviteUser_ReturnsTemporaryPasswordThatLogsIn()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var email = $"invited-{Guid.NewGuid():N}@quizapp.test";

        var inviteResponse = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest(email, "Invited User", [Roles.Scorer]));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invited = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invited!.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        var loginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, invited.TemporaryPassword));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InviteUser_DuplicateEmail_ReturnsConflict()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var email = $"dup-{Guid.NewGuid():N}@quizapp.test";
        await admin.PostAsJsonAsync("/api/v1/admin/users", new InviteUserRequest(email, "First", [Roles.Operator]));

        var second = await admin.PostAsJsonAsync("/api/v1/admin/users", new InviteUserRequest(email, "Second", [Roles.Operator]));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InviteUser_UnrecognisedRole_ReturnsValidationError()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);

        var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest($"bad-{Guid.NewGuid():N}@quizapp.test", "Bad", ["NotARole"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_NewPasswordWorks_OldDoesNot()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var email = $"reset-{Guid.NewGuid():N}@quizapp.test";
        var inviteResponse = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest(email, "Reset Me", [Roles.Operator]));
        var invited = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();

        var resetResponse = await admin.PostAsync($"/api/v1/admin/users/{invited!.User.Id}/reset-password", content: null);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reset = await resetResponse.Content.ReadFromJsonAsync<ResetPasswordResponse>();

        var oldLogin = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, invited.TemporaryPassword));
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLogin = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, reset!.TemporaryPassword));
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AssignRoles_ReplaceSemantics_ReflectedInMe()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var programId = await CreateProgramAsync(admin);
        var email = $"assign-{Guid.NewGuid():N}@quizapp.test";
        var inviteResponse = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest(email, "Assign Me", []));
        var invited = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();

        var firstAssign = await admin.PutAsJsonAsync(
            $"/api/v1/admin/users/{invited!.User.Id}/roles",
            new AssignUserRolesRequest(programId, [Roles.ProgramAdmin]));
        firstAssign.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondAssign = await admin.PutAsJsonAsync(
            $"/api/v1/admin/users/{invited.User.Id}/roles",
            new AssignUserRolesRequest(programId, [Roles.Operator]));
        secondAssign.StatusCode.Should().Be(HttpStatusCode.OK);

        var userClient = _factory.CreateClient();
        var login = await userClient.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, invited.TemporaryPassword));
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var meResponse = await userClient.GetAsync("/api/v1/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        var membership = me!.Programs.Should().ContainSingle(p => p.ProgramId == programId).Which;
        membership.RolesInProgram.Should().ContainSingle().Which.Should().Be(Roles.Operator);
    }

    [Fact]
    public async Task DeactivateUser_BlocksSubsequentLoginAndExistingRefreshToken()
    {
        var admin = await CreateClientAsAsync(Roles.SuperAdmin);
        var email = $"deactivate-{Guid.NewGuid():N}@quizapp.test";
        var inviteResponse = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest(email, "Deactivate Me", [Roles.Operator]));
        var invited = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();

        var userClient = _factory.CreateClient();
        var login = await userClient.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, invited!.TemporaryPassword));
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();

        var deactivateResponse = await admin.PostAsync($"/api/v1/admin/users/{invited.User.Id}/deactivate", content: null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginAfter = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, invited.TemporaryPassword));
        loginAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshAfter = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshRequest(tokens!.RefreshToken));
        refreshAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
