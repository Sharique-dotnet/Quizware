using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Api.Contracts.V1.Programs;
using QuizApp.Api.Contracts.V1.Questions;
using QuizApp.Api.Contracts.V1.Questions.Formats;
using QuizApp.Api.Controllers.v1;
using QuizApp.Application.Authorization;
using QuizApp.Domain.QuestionBank;
using QuizApp.Domain.Tournament;
using QuizApp.Domain.Enums;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Persistence;

namespace QuizApp.Api.IntegrationTests;

/// <summary>Phase 6f: QuestionsController's actions are no longer 501
/// stubs. MCQ is the fully-verified reference format (same
/// build-one-format-first strategy the project already uses for the match
/// engine).</summary>
public class QuestionsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public QuestionsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"q-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"q-user-{Guid.NewGuid():N}@quizapp.test";
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

    private static CreateMcqQuestionRequest BuildMcqRequest(string text = "What is 2 + 2?") => new()
    {
        FormatCode = "Mcq",
        QuestionText = text,
        DifficultyLevelId = 2,
        Options = [new OptionDto("3", false, 1, null), new OptionDto("4", true, 2, null)],
    };

    [Fact]
    public async Task CreateMcq_ThenGetById_RoundTrips()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var createResponse = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<McqQuestionResponse>();
        created!.Status.Should().Be("Draft");
        created.Options.Should().HaveCount(2);

        var getResponse = await client.GetAsync($"/api/v1/programs/{programId}/questions/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_UnusedQuestion_CreatesNewVersionAndRemovesOld()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/programs/{programId}/questions/mcq/{created!.Id}", BuildMcqRequest("What is 3 + 3?"));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<McqQuestionResponse>();
        updated!.SupersedesQuestionId.Should().Be(created.Id);

        var oldResponse = await client.GetAsync($"/api/v1/programs/{programId}/questions/{created.Id}");
        oldResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_UsedQuestion_RetiresOldInsteadOfDeleting()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var question = await dbContext.Questions.SingleAsync(q => q.Id == created!.Id);
            question.RecordUsage();
            await dbContext.SaveChangesAsync();
        }

        await client.PutAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq/{created!.Id}", BuildMcqRequest("Revised text"));

        var oldResponse = await client.GetAsync($"/api/v1/programs/{programId}/questions/{created.Id}");
        oldResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var old = await oldResponse.Content.ReadFromJsonAsync<McqQuestionResponse>();
        old!.Status.Should().Be("Retired");
    }

    [Fact]
    public async Task Approve_DraftQuestion_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();

        var response = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/{created!.Id}/approve", new ApproveQuestionRequest(null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await response.Content.ReadFromJsonAsync<McqQuestionResponse>();
        approved!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Approve_AlreadyApproved_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/{created!.Id}/approve", new ApproveQuestionRequest(null));

        var response = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/{created.Id}/approve", new ApproveQuestionRequest(null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_UnusedQuestion_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/questions/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_UsedQuestion_ReturnsConflict()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var question = await dbContext.Questions.SingleAsync(q => q.Id == created!.Id);
            question.RecordUsage();
            await dbContext.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/v1/programs/{programId}/questions/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var created = await (await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest()))
            .Content.ReadFromJsonAsync<McqQuestionResponse>();
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/{created!.Id}/approve", new ApproveQuestionRequest(null));
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest("Another draft question"));

        var response = await client.GetAsync($"/api/v1/programs/{programId}/questions?status=Approved");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<QuestionSummaryResponse>>();
        results!.Should().ContainSingle(q => q.Id == created.Id);
    }

    [Fact]
    public async Task Coverage_ShortOnApprovedQuestions_ReportsBlocker()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stage = Stage.Create(programId, "League", 1, StageType.League, "test");
            dbContext.Stages.Add(stage);
            dbContext.StageSegmentTemplates.Add(StageSegmentTemplate.Create(programId, stage.Id, QuestionFormatCode.Mcq, 1, 5, "test"));
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/programs/{programId}/questions/coverage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coverage = await response.Content.ReadFromJsonAsync<QuestionCoverageResponse>();
        coverage!.ReadyToRun.Should().BeFalse();
        coverage.FormatsInUse.Should().Contain("Mcq");
        coverage.Blockers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Duplicates_SimilarQuestionText_ReturnsPair()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest("What is the capital of France, exactly"));
        await client.PostAsJsonAsync($"/api/v1/programs/{programId}/questions/mcq", BuildMcqRequest("What is the capital of France"));

        var response = await client.GetAsync($"/api/v1/programs/{programId}/questions/duplicates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicates = await response.Content.ReadFromJsonAsync<QuestionDuplicatesResponse>();
        duplicates!.Pairs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ImportValidate_ThenCommit_CreatesQuestion()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Questions");
        string[] headers = ["QuestionText", "DifficultyLevelId", "Language", "Option1", "Option1Correct", "Option2", "Option2Correct"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Cell(2, 1).Value = "Imported question?";
        sheet.Cell(2, 2).Value = "2";
        sheet.Cell(2, 3).Value = "ur";
        sheet.Cell(2, 4).Value = "Yes";
        sheet.Cell(2, 5).Value = "true";
        sheet.Cell(2, 6).Value = "No";
        sheet.Cell(2, 7).Value = "false";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", "questions.xlsx");

        var validateResponse = await client.PostAsync($"/api/v1/programs/{programId}/questions/import/mcq/validate", content);
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var validated = await validateResponse.Content.ReadFromJsonAsync<QuestionImportValidateResponse>();
        validated!.ValidRowCount.Should().Be(1);

        var commitResponse = await client.PostAsync($"/api/v1/programs/{programId}/questions/import/{validated.BatchId}/commit", content: null);

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var committed = await commitResponse.Content.ReadFromJsonAsync<QuestionImportCommitResponse>();
        committed!.QuestionsCreated.Should().Be(1);
    }
}
