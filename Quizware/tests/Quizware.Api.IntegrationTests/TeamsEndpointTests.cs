using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Contracts.V1.Teams;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 6c: TeamsController's 10 actions are no longer 501
/// stubs. Every route uses {programId}, so ProgramScopeMiddleware applies
/// — tests mint a program-scoped token directly via IJwtTokenService,
/// mirroring ControllerStubReachabilityTests' established pattern.</summary>
public class TeamsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"teams-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"teams-user-{Guid.NewGuid():N}@quizapp.test";
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

    private async Task<Guid> CreateProgramAsync(HttpClient superAdminClient, int? maxTeams = null)
    {
        var response = await superAdminClient.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest($"P-{Guid.NewGuid():N}", "Test Program", null));
        var program = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();

        if (maxTeams is not null)
        {
            await superAdminClient.PutAsJsonAsync(
                $"/api/v1/programs/{program!.Id}",
                new UpdateProgramRequest("Test Program", null, null, null, null, null, null, maxTeams));
        }

        return program!.Id;
    }

    [Fact]
    public async Task Create_ThenGetById_RoundTrips()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams",
            new CreateTeamRequest("T-1", "Al Hamd School", "الحمد", ["Ali", "Sara"]));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var team = await createResponse.Content.ReadFromJsonAsync<TeamDetailResponse>();
        team!.Status.Should().Be("Registered");
        team.Members.Should().HaveCount(2);

        var getResponse = await client.GetAsync($"/api/v1/programs/{programId}/teams/{team.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-DUP", "School A", "Team A", []));

        var second = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-DUP", "School B", "Team B", []));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_BeyondMaxTeams_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin, maxTeams: 1);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", []));

        var second = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-2", "School B", "Team B", []));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_ChangesDetails()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/teams/{created!.Id}",
            new UpdateTeamRequest("Renamed School", "Renamed Team", "RT", "Contact", "123", "c@test.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TeamDetailResponse>();
        updated!.SchoolName.Should().Be("Renamed School");
        updated.DisplayName.Should().Be("Renamed Team");
    }

    [Fact]
    public async Task SetImages_PersistsUrls()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams/{created!.Id}/images",
            new SetTeamImagesRequest("https://cdn.test/score.png", "https://cdn.test/selection.png"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TeamDetailResponse>();
        updated!.ScoreImageUrl.Should().Be("https://cdn.test/score.png");
    }

    [Fact]
    public async Task ChangeStatus_BadStatus_ReturnsValidationError()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams/{created!.Id}/status",
            new ChangeTeamStatusRequest("NotAStatus", "Because"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_ValidStatus_PersistsReasonAndTimestamp()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams/{created!.Id}/status",
            new ChangeTeamStatusRequest("Withdrawn", "Ran out of participants"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TeamDetailResponse>();
        updated!.Status.Should().Be("Withdrawn");
        updated.StatusReason.Should().Be("Ran out of participants");
        updated.StatusChangedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_NeverPlayed_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/teams/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AlreadyPlayed_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stage = Stage.Create(programId, "League", 1, StageType.League, "test");
            dbContext.Stages.Add(stage);
            var match = Match.Create(programId, stage.Id, 1, 42L, "test");
            dbContext.Matches.Add(match);
            dbContext.MatchParticipants.Add(MatchParticipant.Create(programId, match.Id, created!.Id, 1, 1, "test"));
            await dbContext.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/teams/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();
        await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams/{created!.Id}/status",
            new ChangeTeamStatusRequest("Withdrawn", "reason"));
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-2", "School B", "Team B", []));

        var response = await client.GetAsync($"/api/v1/programs/{programId}/teams?status=Withdrawn");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var teams = await response.Content.ReadFromJsonAsync<List<TeamSummaryResponse>>();
        teams!.Should().ContainSingle().Which.Code.Should().Be("T-1");
    }

    [Fact]
    public async Task History_NewTeam_ReturnsEmptyList()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var created = await (await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/teams", new CreateTeamRequest("T-1", "School A", "Team A", [])))
            .Content.ReadFromJsonAsync<TeamDetailResponse>();

        var response = await client.GetAsync($"/api/v1/programs/{programId}/teams/{created!.Id}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<TeamHistoryResponse>();
        history!.Matches.Should().BeEmpty();
    }

    private static MultipartFormDataContent BuildImportFile((string Code, string SchoolName, string DisplayName)[] validRows, bool includeInvalidRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Teams");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "SchoolName";
        sheet.Cell(1, 3).Value = "DisplayName";

        var row = 2;
        foreach (var (code, schoolName, displayName) in validRows)
        {
            sheet.Cell(row, 1).Value = code;
            sheet.Cell(row, 2).Value = schoolName;
            sheet.Cell(row, 3).Value = displayName;
            row++;
        }

        if (includeInvalidRow)
        {
            sheet.Cell(row, 1).Value = "T-BAD";
            sheet.Cell(row, 2).Value = string.Empty;
            sheet.Cell(row, 3).Value = "Bad Row";
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "teams.xlsx");
        return content;
    }

    [Fact]
    public async Task ImportValidate_MixedRows_ReportsEveryRow()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var content = BuildImportFile([("T-IMP1", "Import School 1", "Import Team 1")], includeInvalidRow: true);

        var response = await client.PostAsync($"/api/v1/programs/{programId}/teams/import/validate", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TeamImportValidateResponse>();
        result!.RowCount.Should().Be(2);
        result.ValidRowCount.Should().Be(1);
        result.InvalidRowCount.Should().Be(1);
        result.Rows.Should().Contain(r => !r.IsValid && r.Errors.Any(e => e.Contains("SchoolName")));
    }

    [Fact]
    public async Task ImportValidate_ThenCommit_CreatesOnlyValidRows()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var content = BuildImportFile(
            [("T-C1", "Commit School 1", "Commit Team 1"), ("T-C2", "Commit School 2", "Commit Team 2")],
            includeInvalidRow: true);

        var validateResponse = await client.PostAsync($"/api/v1/programs/{programId}/teams/import/validate", content);
        var validated = await validateResponse.Content.ReadFromJsonAsync<TeamImportValidateResponse>();

        var commitResponse = await client.PostAsync($"/api/v1/programs/{programId}/teams/import/{validated!.BatchId}/commit", content: null);

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var committed = await commitResponse.Content.ReadFromJsonAsync<TeamImportCommitResponse>();
        committed!.TeamsCreated.Should().Be(2);

        var listResponse = await client.GetAsync($"/api/v1/programs/{programId}/teams");
        var teams = await listResponse.Content.ReadFromJsonAsync<List<TeamSummaryResponse>>();
        teams!.Should().Contain(t => t.Code == "T-C1").And.Contain(t => t.Code == "T-C2");
    }

    [Fact]
    public async Task ImportCommit_AlreadyCommittedBatch_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.SuperAdmin);
        var content = BuildImportFile([("T-ONCE", "Once School", "Once Team")], includeInvalidRow: false);
        var validateResponse = await client.PostAsync($"/api/v1/programs/{programId}/teams/import/validate", content);
        var validated = await validateResponse.Content.ReadFromJsonAsync<TeamImportValidateResponse>();
        await client.PostAsync($"/api/v1/programs/{programId}/teams/import/{validated!.BatchId}/commit", content: null);

        var secondCommit = await client.PostAsync($"/api/v1/programs/{programId}/teams/import/{validated.BatchId}/commit", content: null);

        secondCommit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
