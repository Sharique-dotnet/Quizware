using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Contracts.V1.Stages;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Infrastructure.Identity;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 7: StagesController's Stage/segment CRUD, reorder, and
/// validate actions are no longer 501 stubs.</summary>
public class StagesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StagesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"stages-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"stages-user-{Guid.NewGuid():N}@quizapp.test";
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

    private async Task<StageDetailResponse> CreateStageAsync(HttpClient client, Guid programId, string name, int orderIndex)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages", new CreateStageRequest(name, orderIndex));
        return (await response.Content.ReadFromJsonAsync<StageDetailResponse>())!;
    }

    [Fact]
    public async Task Create_ProgramScoped_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages", new CreateStageRequest("League", 0));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stage = await response.Content.ReadFromJsonAsync<StageDetailResponse>();
        stage!.Name.Should().Be("League");
        stage.State.Should().Be("Draft");
    }

    [Fact]
    public async Task Create_DuplicateOrderIndex_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        await CreateStageAsync(client, programId, "League", 0);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages", new CreateStageRequest("Also League", 0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reorder_PartialList_ReturnsBadRequest()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var s1 = await CreateStageAsync(client, programId, "League", 0);
        await CreateStageAsync(client, programId, "Final", 1);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/reorder", new ReorderStagesRequest([s1.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reorder_FullList_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var s1 = await CreateStageAsync(client, programId, "League", 0);
        var s2 = await CreateStageAsync(client, programId, "Final", 1);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/reorder", new ReorderStagesRequest([s2.Id, s1.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stages = await response.Content.ReadFromJsonAsync<List<StageSummaryResponse>>();
        stages!.Single(s => s.Id == s2.Id).OrderIndex.Should().Be(0);
        stages!.Single(s => s.Id == s1.Id).OrderIndex.Should().Be(1);
    }

    [Fact]
    public async Task AddSegment_ThenValidate_IsRunnable()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stage = await CreateStageAsync(client, programId, "League", 0);

        var beforeValidate = await client.PostAsync($"/api/v1/programs/{programId}/stages/{stage.Id}/validate", null);
        var before = await beforeValidate.Content.ReadFromJsonAsync<StageValidationResponse>();
        before!.IsRunnable.Should().BeFalse("a stage with no segments cannot run");

        var segmentResponse = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest("Mcq", 5));
        segmentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterValidate = await client.PostAsync($"/api/v1/programs/{programId}/stages/{stage.Id}/validate", null);
        var after = await afterValidate.Content.ReadFromJsonAsync<StageValidationResponse>();
        after!.IsRunnable.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderSegments_PartialList_ReturnsBadRequest()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stage = await CreateStageAsync(client, programId, "League", 0);
        var seg1 = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest("Mcq", 5)))
            .Content.ReadFromJsonAsync<StageSegmentTemplateDto>();
        await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest("Buzzer", 3));

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments/reorder",
            new ReorderSegmentTemplatesRequest([seg1!.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReorderSegments_LockedSegmentKeepsItsIndex()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stage = await CreateStageAsync(client, programId, "League", 0);
        var locked = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest("Mcq", 5, IsOrderLocked: true)))
            .Content.ReadFromJsonAsync<StageSegmentTemplateDto>();
        var unlocked = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest("Buzzer", 3)))
            .Content.ReadFromJsonAsync<StageSegmentTemplateDto>();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/stages/{stage.Id}/segments/reorder",
            new ReorderSegmentTemplatesRequest([unlocked!.Id, locked!.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ReorderSegmentTemplatesResponse>();
        result!.Segments.Single(s => s.Id == locked.Id).OrderIndex.Should().Be(0, "locked segments keep their original index");
    }

    [Fact]
    public async Task Delete_StageWithNoMatches_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stage = await CreateStageAsync(client, programId, "League", 0);

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/stages/{stage.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
