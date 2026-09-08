using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Contracts.V1.Auth;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Api.Controllers.v1;
using QuizApp.Application.Authorization;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Persistence;

namespace QuizApp.Api.IntegrationTests;

/// <summary>Phase 6b: select-program, display-token, me, change-password
/// and logout are no longer 501 stubs.</summary>
public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId, string Password)> CreateClientAsAsync(params string[] roles)
    {
        var email = $"auth-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";
        Guid userId;

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

            userId = user.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return (client, userId, password);
    }

    private async Task<Guid> CreateProgramAsync(HttpClient superAdminClient)
    {
        var response = await superAdminClient.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest($"P-{Guid.NewGuid():N}", "Test Program", null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var program = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        return program!.Id;
    }

    private async Task GrantProgramRoleAsync(Guid userId, Guid programId, string roleName)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await roleManager.FindByNameAsync(roleName);

        dbContext.ProgramUsers.Add(new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            UserId = userId,
            RoleId = role!.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        });
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Me_UnscopedUser_ReturnsGlobalRolesAndNoPrograms()
    {
        var (client, _, _) = await CreateClientAsAsync(Roles.Operator);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        me!.Roles.Should().Contain(Roles.Operator);
        me.Programs.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectProgram_AsSuperAdmin_SucceedsWithoutProgramUserRow()
    {
        var (client, _, _) = await CreateClientAsAsync(Roles.SuperAdmin);
        var programId = await CreateProgramAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/auth/select-program", new SelectProgramRequest(programId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token!.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type == "program_id" && c.Value == programId.ToString());
        jwt.Claims.Should().Contain(c => c.Value == Roles.SuperAdmin);
    }

    [Fact]
    public async Task SelectProgram_WithNoMembership_ReturnsForbidden()
    {
        var (superAdmin, _, _) = await CreateClientAsAsync(Roles.SuperAdmin);
        var programId = await CreateProgramAsync(superAdmin);
        var (client, _, _) = await CreateClientAsAsync(Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/auth/select-program", new SelectProgramRequest(programId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelectProgram_WithProgramUserRow_TokenCarriesThatProgramsRoleOnly()
    {
        var (superAdmin, _, _) = await CreateClientAsAsync(Roles.SuperAdmin);
        var programId = await CreateProgramAsync(superAdmin);
        var (client, userId, _) = await CreateClientAsAsync(Roles.Auditor);
        await GrantProgramRoleAsync(userId, programId, Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/auth/select-program", new SelectProgramRequest(programId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token!.AccessToken);
        var roleClaims = jwt.Claims.Where(c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().ContainSingle().Which.Should().Be(Roles.Operator);
    }

    [Fact]
    public async Task DisplayToken_Minted_GetsForbiddenOnAWriteEndpoint()
    {
        var (superAdmin, _, _) = await CreateClientAsAsync(Roles.SuperAdmin);
        var programId = await CreateProgramAsync(superAdmin);

        var mintResponse = await superAdmin.PostAsJsonAsync(
            "/api/v1/auth/display-token", new DisplayTokenRequest(programId, 60));
        mintResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var display = await mintResponse.Content.ReadFromJsonAsync<DisplayTokenResponse>();

        var displayClient = _factory.CreateClient();
        displayClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", display!.DisplayToken);

        var writeResponse = await displayClient.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest("X", "X", null));

        writeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Fails()
    {
        var (client, _, _) = await CreateClientAsAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/change-password", new ChangePasswordRequest("WrongPassword1!", "NewPassword1!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_Succeeds()
    {
        var (client, _, password) = await CreateClientAsAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/change-password", new ChangePasswordRequest(password, "NewPassword1!"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_SubsequentRefreshFails()
    {
        var email = $"logout-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);
        }

        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
