using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Api.Contracts.V1.Topics;
using QuizApp.Api.Controllers.v1;
using QuizApp.Application.Authorization;
using QuizApp.Infrastructure.Identity;

namespace QuizApp.Api.IntegrationTests;

/// <summary>Phase 6d: TagsController's 5 actions are no longer 501 stubs.</summary>
public class TagsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TagsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"tags-admin-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Test Admin", IsActive = true, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRolesAsync(user, [Roles.SuperAdmin]);
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateProgramScopedClientAsync(Guid programId, params string[] roles)
    {
        var email = $"tags-user-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true, EmailConfirmed = true };
        await userManager.CreateAsync(user, password);

        var token = tokenService.GenerateAccessToken(user, roles, programId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
    public async Task Create_ProgramScoped_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/tags", new CreateTagRequest("poetry"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>();
        tag!.ProgramId.Should().Be(programId);
    }

    [Fact]
    public async Task Create_DuplicateNameInSameScope_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/tags", new CreateTagRequest("ghalib"));

        var second = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/tags", new CreateTagRequest("ghalib"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_SharedAsNonSuperAdmin_ReturnsForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/tags", new CreateTagRequest("shared-tag", Shared: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ReturnsOwnAndSharedTags()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var superAdminScoped = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        await superAdminScoped.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/tags", new CreateTagRequest("shared-tag", Shared: true));
        var programClient = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        await programClient.PostAsJsonAsync($"/api/v1/programs/{programId}/tags", new CreateTagRequest("own-tag"));

        var response = await programClient.GetAsync($"/api/v1/programs/{programId}/tags");

        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>();
        tags!.Should().Contain(t => t.Name == "shared-tag" && t.ProgramId == null);
        tags.Should().Contain(t => t.Name == "own-tag" && t.ProgramId == programId);
    }

    [Fact]
    public async Task Delete_OwnTag_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/tags", new CreateTagRequest("removable")))
            .Content.ReadFromJsonAsync<TagResponse>();

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/tags/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_SharedTagAsNonSuperAdmin_ReturnsForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var superAdminScoped = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await superAdminScoped.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/tags", new CreateTagRequest("shared-removable", Shared: true)))
            .Content.ReadFromJsonAsync<TagResponse>();

        var programClient = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var response = await programClient.DeleteAsync($"/api/v1/programs/{programId}/tags/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
