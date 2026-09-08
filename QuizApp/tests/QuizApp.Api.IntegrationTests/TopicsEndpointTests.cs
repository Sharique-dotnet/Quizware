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

/// <summary>Phase 6d: TopicsController's 5 actions are no longer 501
/// stubs. Routes use {programId} — ProgramScopeMiddleware applies, so
/// tests mint a program-scoped token directly, same as
/// TeamsEndpointTests.cs.</summary>
public class TopicsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TopicsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"topics-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"topics-user-{Guid.NewGuid():N}@quizapp.test";
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

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("History", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var topic = await response.Content.ReadFromJsonAsync<TopicResponse>();
        topic!.ProgramId.Should().Be(programId);
    }

    [Fact]
    public async Task Create_SharedAsNonSuperAdmin_ReturnsForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Science", null, Shared: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_SharedAsSuperAdmin_HasNullProgramId()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("General Knowledge", null, Shared: true));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var topic = await response.Content.ReadFromJsonAsync<TopicResponse>();
        topic!.ProgramId.Should().BeNull();
    }

    [Fact]
    public async Task List_ReturnsOwnAndSharedTopics()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var superAdminScoped = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        await superAdminScoped.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Shared Topic", null, Shared: true));
        var programClient = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        await programClient.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Own Topic", null));

        var response = await programClient.GetAsync($"/api/v1/programs/{programId}/topics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var topics = await response.Content.ReadFromJsonAsync<List<TopicResponse>>();
        topics!.Should().Contain(t => t.Name == "Shared Topic" && t.ProgramId == null);
        topics.Should().Contain(t => t.Name == "Own Topic" && t.ProgramId == programId);
    }

    [Fact]
    public async Task Update_SharedTopicAsNonSuperAdmin_ReturnsForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var superAdminScoped = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await superAdminScoped.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Shared", null, Shared: true)))
            .Content.ReadFromJsonAsync<TopicResponse>();

        var programClient = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var response = await programClient.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/topics/{created!.Id}", new UpdateTopicRequest("Renamed", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_SelfParent_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Root", null)))
            .Content.ReadFromJsonAsync<TopicResponse>();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/topics/{created!.Id}", new UpdateTopicRequest("Root", created.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_CreatesCycle_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var parent = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Parent", null)))
            .Content.ReadFromJsonAsync<TopicResponse>();
        var child = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Child", parent!.Id)))
            .Content.ReadFromJsonAsync<TopicResponse>();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/topics/{parent.Id}", new UpdateTopicRequest("Parent", child!.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_TopicWithChildren_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var parent = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Parent", null)))
            .Content.ReadFromJsonAsync<TopicResponse>();
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Child", parent!.Id));

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/topics/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_LeafTopic_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/topics", new CreateTopicRequest("Leaf", null)))
            .Content.ReadFromJsonAsync<TopicResponse>();

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/topics/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
