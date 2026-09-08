using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 6a: ProgramsController's 14 actions are no longer 501
/// stubs. None of these routes need program-scope (ADR-002's
/// {programId}-must-match-token rule doesn't apply — the routes use
/// {id:guid}), so every test here uses a plain, unscoped token.</summary>
public class ProgramsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProgramsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientAsAsync(params string[] roles)
    {
        var email = $"programs-{Guid.NewGuid():N}@quizapp.test";
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<ProgramDetailResponse> CreateProgramAsync(HttpClient client, string? code = null)
    {
        var request = new CreateProgramRequest(code ?? $"P-{Guid.NewGuid():N}", "Inter-School Quiz", "The annual tournament");
        var response = await client.PostAsJsonAsync("/api/v1/programs", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProgramDetailResponse>())!;
    }

    private async Task SeedStageWithSegmentAsync(Guid programId, QuestionFormatCode formatCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stage = Stage.Create(programId, "League Stage", 1, StageType.League, "test");
        dbContext.Stages.Add(stage);
        dbContext.StageSegmentTemplates.Add(StageSegmentTemplate.Create(programId, stage.Id, formatCode, 1, 10, "test"));
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_AsSuperAdmin_SeedsEveryQuestionFormatEnabled()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);

        var program = await CreateProgramAsync(client);
        program.Code.Should().NotBeNullOrWhiteSpace();
        program.State.Should().Be("Draft");

        var formatsResponse = await client.GetAsync($"/api/v1/programs/{program.Id}/formats");
        formatsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var formats = await formatsResponse.Content.ReadFromJsonAsync<ProgramFormatsResponse>();

        formats!.Formats.Should().HaveCount(Enum.GetValues<QuestionFormatCode>().Length);
        formats.Formats.Should().OnlyContain(f => f.IsEnabled);
    }

    [Fact]
    public async Task GetById_UnknownProgram_ReturnsNotFound()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);

        var response = await client.GetAsync($"/api/v1/programs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ChangesNameDescriptionAndBranding()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var update = new UpdateProgramRequest(
            "Renamed Quiz", "Updated description", "Acme Schools Trust", "https://cdn.test/logo.png", "#112233", "#445566", "Inter", 20);
        var response = await client.PutAsJsonAsync($"/api/v1/programs/{program.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        updated!.Name.Should().Be("Renamed Quiz");
        updated.OrganisationName.Should().Be("Acme Schools Trust");
        updated.LogoUrl.Should().Be("https://cdn.test/logo.png");
        updated.MaxTeams.Should().Be(20);
    }

    [Fact]
    public async Task List_ReturnsCreatedProgram()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin, Roles.ProgramAdmin);
        var program = await CreateProgramAsync(client);

        var response = await client.GetAsync("/api/v1/programs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<ProgramSummaryResponse>>();
        list!.Should().Contain(p => p.Id == program.Id);
    }

    [Fact]
    public async Task Settings_UpdateThenGet_UpsertsAndPersists()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var first = await client.PutAsJsonAsync(
            $"/api/v1/programs/{program.Id}/settings",
            new UpdateProgramSettingsRequest([new ProgramSettingEntry("Teams", "MaxTeams", "20")]));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutAsJsonAsync(
            $"/api/v1/programs/{program.Id}/settings",
            new UpdateProgramSettingsRequest([new ProgramSettingEntry("Teams", "MaxTeams", "24")]));
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/v1/programs/{program.Id}/settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<ProgramSettingsResponse>();

        settings!.Settings.Should().ContainSingle(s => s.Category == "Teams" && s.Key == "MaxTeams" && s.Value == "24");
    }

    [Fact]
    public async Task Formats_DisableThenEnable_RoundTrips()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var disableResponse = await client.PutAsJsonAsync(
            $"/api/v1/programs/{program.Id}/formats",
            new UpdateProgramFormatsRequest([new ProgramFormatEntry("Sequence", false, null, "Not used this season")]));
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<ProgramFormatsResponse>();
        disabled!.Formats.Single(f => f.FormatCode == "Sequence").IsEnabled.Should().BeFalse();

        var enableResponse = await client.PutAsJsonAsync(
            $"/api/v1/programs/{program.Id}/formats",
            new UpdateProgramFormatsRequest([new ProgramFormatEntry("Sequence", true, null, null)]));
        var enabled = await enableResponse.Content.ReadFromJsonAsync<ProgramFormatsResponse>();
        enabled!.Formats.Single(f => f.FormatCode == "Sequence").IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Formats_DisableFormatStillUsedByASegmentTemplate_ReturnsConflictNamingTheStage()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);
        await SeedStageWithSegmentAsync(program.Id, QuestionFormatCode.Mcq);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{program.Id}/formats",
            new UpdateProgramFormatsRequest([new ProgramFormatEntry("Mcq", false, null, "Trying to disable")]));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("FORMAT_IN_USE").And.Contain("League Stage");
    }

    [Fact]
    public async Task Clone_CopiesSettingsAndFormats_NewProgramIsDraft()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var source = await CreateProgramAsync(client);
        await client.PutAsJsonAsync(
            $"/api/v1/programs/{source.Id}/settings",
            new UpdateProgramSettingsRequest([new ProgramSettingEntry("Teams", "MaxTeams", "20")]));
        await client.PutAsJsonAsync(
            $"/api/v1/programs/{source.Id}/formats",
            new UpdateProgramFormatsRequest([new ProgramFormatEntry("Sequence", false, null, "Not used")]));

        var cloneResponse = await client.PostAsJsonAsync(
            $"/api/v1/programs/{source.Id}/clone",
            new CloneProgramRequest(source.Id, $"C-{Guid.NewGuid():N}", "Cloned Quiz"));

        cloneResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var clone = await cloneResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        clone!.State.Should().Be("Draft");

        var settings = await (await client.GetAsync($"/api/v1/programs/{clone.Id}/settings"))
            .Content.ReadFromJsonAsync<ProgramSettingsResponse>();
        settings!.Settings.Should().ContainSingle(s => s.Category == "Teams" && s.Key == "MaxTeams" && s.Value == "20");

        var formats = await (await client.GetAsync($"/api/v1/programs/{clone.Id}/formats"))
            .Content.ReadFromJsonAsync<ProgramFormatsResponse>();
        formats!.Formats.Single(f => f.FormatCode == "Sequence").IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NoStages_ReturnsNotReadyWithBlocker()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var response = await client.PostAsync($"/api/v1/programs/{program.Id}/validate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProgramValidationResponse>();
        result!.ReadyToGoLive.Should().BeFalse();
        result.Blockers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Activate_NoStages_ReturnsConflict()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var response = await client.PostAsync($"/api/v1/programs/{program.Id}/activate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_ThenComplete_ThenArchive_TransitionsThroughLifecycle()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);
        await SeedStageWithSegmentAsync(program.Id, QuestionFormatCode.Mcq);

        var activateResponse = await client.PostAsync($"/api/v1/programs/{program.Id}/activate", content: null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activated = await activateResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        activated!.State.Should().Be("Live");

        var completeResponse = await client.PostAsync($"/api/v1/programs/{program.Id}/complete", content: null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await completeResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>())!.State.Should().Be("Completed");

        var archiveResponse = await client.PostAsync($"/api/v1/programs/{program.Id}/archive", content: null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await archiveResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>())!.State.Should().Be("Archived");
    }

    [Fact]
    public async Task Dashboard_NewProgram_ReturnsZeroCounts()
    {
        var client = await CreateClientAsAsync(Roles.SuperAdmin);
        var program = await CreateProgramAsync(client);

        var response = await client.GetAsync($"/api/v1/programs/{program.Id}/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<ProgramDashboardResponse>();
        dashboard!.TeamCount.Should().Be(0);
        dashboard.MatchCount.Should().Be(0);
    }
}
