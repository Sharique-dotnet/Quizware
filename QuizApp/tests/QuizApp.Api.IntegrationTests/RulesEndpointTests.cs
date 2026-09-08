using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Api.Contracts.V1.Rules;
using QuizApp.Api.Contracts.V1.Stages;
using QuizApp.Api.Controllers.v1;
using QuizApp.Application.Authorization;
using QuizApp.Infrastructure.Identity;

namespace QuizApp.Api.IntegrationTests;

/// <summary>Phase 7: RulesController's scoring/selection/qualification/
/// tie-break actions are no longer 501 stubs.</summary>
public class RulesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RulesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"rules-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"rules-user-{Guid.NewGuid():N}@quizapp.test";
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

    private async Task<Guid> CreateStageAsync(HttpClient client, Guid programId, string name, int orderIndex)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/stages", new CreateStageRequest(name, orderIndex));
        var stage = await response.Content.ReadFromJsonAsync<StageDetailResponse>();
        return stage!.Id;
    }

    [Fact]
    public async Task UpsertScoring_ThenGet_RoundTrips()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var upsert = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/scoring",
            new UpsertScoringRulesRequest([new ScoringRuleDto(Guid.Empty, "Mcq", "Correct", null, 10)]));

        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"/api/v1/programs/{programId}/rules/scoring");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await get.Content.ReadFromJsonAsync<List<ScoringRuleDto>>();
        rules!.Should().Contain(r => r.FormatCode == "Mcq" && r.Outcome == "Correct" && r.Points == 10);
    }

    [Fact]
    public async Task ResetScoringDefaults_SeedsLegacyValues()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsync($"/api/v1/programs/{programId}/rules/scoring/reset-defaults", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await response.Content.ReadFromJsonAsync<List<ScoringRuleDto>>();
        rules!.Should().Contain(r => r.FormatCode == "Mcq" && r.Outcome == "Correct" && r.Points == 10);
        rules.Should().Contain(r => r.FormatCode == "Buzzer" && r.Outcome == "Incorrect" && r.Points == -15);
    }

    [Fact]
    public async Task UpsertTieBreak_ThenGet_RoundTrips()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stageId = await CreateStageAsync(client, programId, "League", 0);

        var upsert = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/tie-break",
            new UpsertTieBreakRulesRequest([new TieBreakRuleDto(Guid.Empty, stageId, ["HeadToHead"], "Mcq", 3, false, 3, false, "ManualDecision")]));

        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"/api/v1/programs/{programId}/rules/tie-break");
        var rules = await get.Content.ReadFromJsonAsync<List<TieBreakRuleDto>>();
        rules!.Should().Contain(r => r.StageId == stageId && r.QuestionCount == 3);
    }

    [Fact]
    public async Task ResetTieBreakDefaults_SeedsThreeRows()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsync($"/api/v1/programs/{programId}/rules/tie-break/reset-defaults", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await response.Content.ReadFromJsonAsync<List<TieBreakRuleDto>>();
        rules!.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpsertQualification_ThenGet_RoundTrips()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stageId = await CreateStageAsync(client, programId, "League", 0);

        var upsert = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/qualification",
            new UpsertQualificationRulesRequest([new QualificationRuleDto(Guid.Empty, stageId, 1, 2, 0)]));

        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"/api/v1/programs/{programId}/rules/qualification");
        var rules = await get.Content.ReadFromJsonAsync<List<QualificationRuleDto>>();
        rules!.Should().Contain(r => r.StageId == stageId && r.WinnersPerMatch == 1 && r.BestRemainingAcrossStage == 2);
    }

    [Fact]
    public async Task PreviewSelection_EmptyPool_CannotSatisfy()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var stageId = await CreateStageAsync(client, programId, "League", 0);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/selection/preview", new SelectionPreviewRequest(stageId, "Mcq", 5));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SelectionPreviewResponse>();
        result!.CanSatisfy.Should().BeFalse();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RulesEndpoints_WithoutCanManageProgram_AreForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.Operator);

        var response = await client.GetAsync($"/api/v1/programs/{programId}/rules/scoring");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
