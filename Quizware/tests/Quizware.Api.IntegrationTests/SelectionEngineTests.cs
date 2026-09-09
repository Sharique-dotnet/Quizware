using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Contracts.V1.Rules;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Application.Selection;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 8: <see cref="IQuestionSelector"/> — pool building,
/// repeat-policy exclusion, seeded weighted draw, reservation, and the
/// fallback ladder.</summary>
public class SelectionEngineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SelectionEngineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"selection-admin-{Guid.NewGuid():N}@quizapp.test";
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
        var email = $"selection-user-{Guid.NewGuid():N}@quizapp.test";
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

    private static async Task<Question> SeedApprovedMcqAsync(
        AppDbContext db, Guid programId, DifficultyLevel difficulty = DifficultyLevel.Medium, Guid? topicId = null, int timesUsed = 0)
    {
        var question = McqQuestion.Create(
            programId, QuestionOwnerScope.Program, $"Q-{Guid.NewGuid():N}", difficulty, "en", "test", topicId);
        question.Approve(Guid.NewGuid());
        for (var i = 0; i < timesUsed; i++)
        {
            question.RecordUsage();
        }

        db.Questions.Add(question);
        db.QuestionOptions.Add(QuestionOption.Create(question.Id, "A", true, 0, "test"));
        db.QuestionOptions.Add(QuestionOption.Create(question.Id, "B", false, 1, "test"));
        await db.SaveChangesAsync();
        return question;
    }

    [Fact]
    public async Task Preview_NoApprovedQuestions_ReportsCannotSatisfy()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/selection/preview",
            new SelectionPreviewRequest(Guid.NewGuid(), "Mcq", 5));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SelectionPreviewResponse>();
        result!.CanSatisfy.Should().BeFalse();
        result.Warnings.Should().NotBeEmpty();
        result.PoolSize.Should().Be(0);
    }

    [Fact]
    public async Task Preview_EnoughApprovedQuestions_ReportsCanSatisfy()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 5; i++)
            {
                await SeedApprovedMcqAsync(db, programId);
            }
        }

        var response = await client.PostAsJsonAsync(
            $"/api/v1/programs/{programId}/rules/selection/preview",
            new SelectionPreviewRequest(Guid.NewGuid(), "Mcq", 3));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SelectionPreviewResponse>();
        result!.CanSatisfy.Should().BeTrue();
        result.PoolSize.Should().Be(5);
        result.EligibleAfterFilters.Should().Be(5);
    }

    [Fact]
    public async Task SameSeed_ProducesIdenticalDraw()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<IQuestionSelector>();
        var programId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            await SeedApprovedMcqAsync(db, programId, timesUsed: i);
        }

        var request = new SelectionRequest(programId, Guid.NewGuid(), null, QuestionFormatCode.Mcq, 4, RandomSeed: 42);

        var first = await selector.PreviewAsync(request, CancellationToken.None);
        var second = await selector.PreviewAsync(request, CancellationToken.None);

        first.Questions.Select(q => q.QuestionId).Should().Equal(second.Questions.Select(q => q.QuestionId));
    }

    [Fact]
    public async Task RepeatPolicy_NeverInProgram_ExcludesPreviouslyUsedQuestion()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<IQuestionSelector>();
        var programId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        var used = await SeedApprovedMcqAsync(db, programId);
        var fresh = await SeedApprovedMcqAsync(db, programId);

        db.QuestionUsageHistories.Add(QuestionUsageHistory.Record(programId, used.Id, Guid.NewGuid(), stageId));
        await db.SaveChangesAsync();

        var request = new SelectionRequest(programId, stageId, null, QuestionFormatCode.Mcq, 1, RandomSeed: 7);
        var result = await selector.PreviewAsync(request, CancellationToken.None);

        result.EligibleAfterRepeatPolicy.Should().Be(1);
        result.Questions.Should().ContainSingle().Which.QuestionId.Should().Be(fresh.Id);
    }

    [Fact]
    public async Task SelectAndReserve_WritesReservedMatchQuestions_AndLocksThemFromOtherMatches()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<IQuestionSelector>();
        var programId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        var q1 = await SeedApprovedMcqAsync(db, programId);
        var q2 = await SeedApprovedMcqAsync(db, programId);

        var matchId = Guid.NewGuid();
        var segmentId = Guid.NewGuid();
        var request = new SelectionRequest(programId, stageId, null, QuestionFormatCode.Mcq, 2, RandomSeed: 3, MatchId: matchId);

        var result = await selector.SelectAndReserveAsync(request, segmentId, "test", CancellationToken.None);
        await db.SaveChangesAsync();

        result.Questions.Should().HaveCount(2);
        result.Questions.Should().OnlyContain(q => q.OptionOrderJson != null);

        var reserved = await db.MatchQuestions.Where(mq => mq.MatchId == matchId).ToListAsync();
        reserved.Should().HaveCount(2);
        reserved.Should().OnlyContain(mq => mq.State == MatchQuestionState.Reserved);

        // A second match trying to draw the same two questions must fail —
        // they are locked to the first match until released.
        var secondMatchRequest = new SelectionRequest(
            programId, stageId, null, QuestionFormatCode.Mcq, 2, RandomSeed: 3, MatchId: Guid.NewGuid());
        var act = async () => await selector.SelectAndReserveAsync(secondMatchRequest, Guid.NewGuid(), "test", CancellationToken.None);

        await act.Should().ThrowAsync<QuestionPoolExhaustedException>();

        // ids referenced only to keep the seeded questions from being an unused warning
        _ = (q1.Id, q2.Id);
    }

    [Fact]
    public async Task ReleaseReservations_FreesQuestionsBackToThePool()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<IQuestionSelector>();
        var programId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        await SeedApprovedMcqAsync(db, programId);
        var matchId = Guid.NewGuid();
        var request = new SelectionRequest(programId, stageId, null, QuestionFormatCode.Mcq, 1, RandomSeed: 9, MatchId: matchId);

        await selector.SelectAndReserveAsync(request, Guid.NewGuid(), "test", CancellationToken.None);
        await db.SaveChangesAsync();

        await selector.ReleaseReservationsAsync(matchId, CancellationToken.None);
        await db.SaveChangesAsync();

        var reReserved = await selector.SelectAndReserveAsync(
            request with { MatchId = Guid.NewGuid() }, Guid.NewGuid(), "test", CancellationToken.None);

        reReserved.Questions.Should().HaveCount(1);
    }

    [Fact]
    public async Task SelectAndReserve_PoolTooSmall_ThrowsQuestionPoolExhausted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<IQuestionSelector>();
        var programId = Guid.NewGuid();

        await SeedApprovedMcqAsync(db, programId);

        var request = new SelectionRequest(
            programId, Guid.NewGuid(), null, QuestionFormatCode.Mcq, 5, RandomSeed: 1, MatchId: Guid.NewGuid());

        var act = async () => await selector.SelectAndReserveAsync(request, Guid.NewGuid(), "test", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<QuestionPoolExhaustedException>();
        exception.Which.Message.Should().Contain("Mcq").And.Contain("required 5").And.Contain("available 1");
    }
}
