using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Contracts.V1.Stages;
using Quizware.Api.Contracts.V1.Teams;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9: a fresh program with a program-scoped ProgramAdmin +
/// Operator client, plus helpers to build stages, teams, questions and
/// matches — shared by every match setup and live-engine test.</summary>
public sealed class MatchTestHarness
{
    private readonly CustomWebApplicationFactory _factory;

    private MatchTestHarness(CustomWebApplicationFactory factory, HttpClient client, Guid programId)
    {
        _factory = factory;
        Client = client;
        ProgramId = programId;
    }

    public HttpClient Client { get; }

    public Guid ProgramId { get; }

    public string MatchesUrl => $"/api/v1/programs/{ProgramId}/matches";

    public static string LiveUrl(Guid matchId) => $"/api/v1/matches/{matchId}/live";

    public static async Task<MatchTestHarness> CreateAsync(CustomWebApplicationFactory factory)
    {
        var superAdmin = await CreateSuperAdminClientAsync(factory);
        var response = await superAdmin.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest($"P-{Guid.NewGuid():N}", "Match Test Program", null));
        response.EnsureSuccessStatusCode();
        var program = (await response.Content.ReadFromJsonAsync<ProgramDetailResponse>())!;

        var client = await CreateScopedClientAsync(factory, program.Id, Roles.ProgramAdmin, Roles.Operator);
        return new MatchTestHarness(factory, client, program.Id);
    }

    public Task<HttpClient> CreateClientAsync(params string[] roles) => CreateScopedClientAsync(_factory, ProgramId, roles);

    public async Task<StageDetailResponse> CreateStageAsync(params (string Format, int Count)[] segments)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/programs/{ProgramId}/stages", new CreateStageRequest($"Stage-{Guid.NewGuid():N}"[..20], NextOrderIndex()));
        response.EnsureSuccessStatusCode();
        var stage = (await response.Content.ReadFromJsonAsync<StageDetailResponse>())!;

        foreach (var (format, count) in segments)
        {
            var segmentResponse = await Client.PostAsJsonAsync(
                $"/api/v1/programs/{ProgramId}/stages/{stage.Id}/segments", new CreateSegmentTemplateRequest(format, count));
            segmentResponse.EnsureSuccessStatusCode();
        }

        return stage;
    }

    public async Task<Guid> CreateTeamAsync(string displayName)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/programs/{ProgramId}/teams",
            new CreateTeamRequest($"T{Guid.NewGuid():N}"[..10], $"{displayName} School", displayName, ["Member A"]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!.Id;
    }

    public async Task<IReadOnlyList<Guid>> CreateTeamsAsync(int count)
    {
        var ids = new List<Guid>();
        for (var i = 1; i <= count; i++)
        {
            ids.Add(await CreateTeamAsync($"Team {i}"));
        }

        return ids;
    }

    public async Task ResetScoringDefaultsAsync()
    {
        var response = await Client.PostAsync($"/api/v1/programs/{ProgramId}/rules/scoring/reset-defaults", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> SeedTopicAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var topic = Topic.Create(name, "test", ProgramId);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic.Id;
    }

    /// <summary>Sets a stage template's play settings, which no API exposes yet.</summary>
    public async Task ConfigureTemplatesAsync(
        Guid stageId, int? timeLimitSeconds = null, TopicSelectionMode topicMode = TopicSelectionMode.None,
        bool allowPassing = false, int? maxPassCount = null, int? topicChoiceLimit = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var template in db.StageSegmentTemplates.Where(t => t.StageId == stageId))
        {
            template.ConfigurePlay(null, timeLimitSeconds, topicMode, topicChoiceLimit, allowPassing, maxPassCount, "test");
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Sets a stage's live-play settings, which no API exposes yet.</summary>
    public async Task ConfigureStageAsync(Guid stageId, TeamCountChangePolicy policy, bool allowReorderDuringMatch = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stage = db.Stages.Single(s => s.Id == stageId);
        stage.ConfigureMatchPlay(policy, allowReorderDuringMatch, "test");
        await db.SaveChangesAsync();
    }

    public async Task<HttpResponseMessage> DisqualifyAsync(StartedMatch started, Guid participantId, bool excludeFromStandings = true) =>
        await Client.PostAsJsonAsync(
            $"{started.Live}/participants/{participantId}/disqualify",
            new Contracts.V1.LiveMatch.DisqualifyParticipantRequest("Rule breach", Guid.NewGuid(), excludeFromStandings));

    public async Task<StartedMatch> StartWithFirstSegmentOpenAsync(MatchDetailResponse match)
    {
        var live = LiveUrl(match.Id);
        (await Client.PostAsync($"{live}/start", null)).EnsureSuccessStatusCode();
        var segments = (await Client.GetFromJsonAsync<MatchSegmentsResponse>($"{MatchesUrl}/{match.Id}/segments"))!.Segments;
        (await Client.PostAsync($"{live}/segments/{segments[0].Id}/open", null)).EnsureSuccessStatusCode();
        return new StartedMatch(match, live, segments);
    }

    public async Task<Contracts.V1.LiveMatch.CurrentQuestionDto> ServeAsync(StartedMatch started, int segmentIndex = 0)
    {
        var response = await Client.PostAsJsonAsync(
            $"{started.Live}/questions/serve", new Contracts.V1.LiveMatch.ServeQuestionRequest(started.Segments[segmentIndex].Id));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Contracts.V1.LiveMatch.CurrentQuestionDto>())!;
    }

    public async Task<Contracts.V1.LiveMatch.LiveMatchStateResponse> StateAsync(StartedMatch started) =>
        (await Client.GetFromJsonAsync<Contracts.V1.LiveMatch.LiveMatchStateResponse>($"{started.Live}/state"))!;

    public async Task<HttpResponseMessage> AnswerAsync(
        StartedMatch started, Guid matchQuestionId, Guid participantId, string outcome, int passNumber = 0,
        Guid? selectedOptionId = null, string? idempotencyKey = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"{started.Live}/answers")
        {
            Content = JsonContent.Create(new Contracts.V1.LiveMatch.RecordAnswerRequest(
                matchQuestionId, participantId, outcome, selectedOptionId, null, null, passNumber, "Operator", null, null)),
        };
        if (idempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await Client.SendAsync(message);
    }

    public async Task AddScoringRuleAsync(string format, string outcome, string? contextKey, int points)
    {
        var response = await Client.PutAsJsonAsync(
            $"/api/v1/programs/{ProgramId}/rules/scoring",
            new Contracts.V1.Rules.UpsertScoringRulesRequest([new Contracts.V1.Rules.ScoringRuleDto(Guid.Empty, format, outcome, contextKey, points)]));
        response.EnsureSuccessStatusCode();
    }

    public async Task SeedBuzzerAsync(int count, bool allowStealAfterWrong = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
        {
            var question = BuzzerQuestion.Create(
                ProgramId, QuestionOwnerScope.Program, $"Buzzer {i} {Guid.NewGuid():N}", DifficultyLevel.Medium, "en", "test",
                allowStealAfterWrong: allowStealAfterWrong);
            question.Approve(Guid.NewGuid());
            db.Questions.Add(question);
        }

        await db.SaveChangesAsync();
    }

    public sealed record StartedMatch(MatchDetailResponse Match, string Live, IReadOnlyList<MatchSegmentSummaryDto> Segments);

    /// <summary>Approved MCQ questions, each with one correct option (A) and
    /// one incorrect option (B).</summary>
    public async Task<IReadOnlyList<Guid>> SeedMcqAsync(int count, Guid? topicId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var question = McqQuestion.Create(
                ProgramId, QuestionOwnerScope.Program, $"MCQ {i} {Guid.NewGuid():N}", DifficultyLevel.Medium, "en", "test", topicId);
            question.Approve(Guid.NewGuid());
            db.Questions.Add(question);
            db.QuestionOptions.Add(QuestionOption.Create(question.Id, "A", true, 0, "test"));
            db.QuestionOptions.Add(QuestionOption.Create(question.Id, "B", false, 1, "test"));
            ids.Add(question.Id);
        }

        await db.SaveChangesAsync();
        return ids;
    }

    public async Task<MatchDetailResponse> CreateMatchAsync(Guid stageId, int matchNumber, IReadOnlyList<Guid> teamIds)
    {
        var response = await Client.PostAsJsonAsync(MatchesUrl, new CreateMatchRequest(stageId, $"Match {matchNumber}", matchNumber));
        response.EnsureSuccessStatusCode();
        var match = (await response.Content.ReadFromJsonAsync<MatchDetailResponse>())!;

        for (var i = 0; i < teamIds.Count; i++)
        {
            var add = await Client.PostAsJsonAsync(
                $"{MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(teamIds[i], i + 1));
            add.EnsureSuccessStatusCode();
        }

        return (await Client.GetFromJsonAsync<MatchDetailResponse>($"{MatchesUrl}/{match.Id}"))!;
    }

    /// <summary>A match with <paramref name="teamCount"/> teams, one MCQ
    /// segment per entry in <paramref name="segmentQuestionCounts"/>, enough
    /// questions, and default scoring — i.e. one that passes the ready check.</summary>
    public async Task<MatchDetailResponse> CreateReadyToStartMatchAsync(int teamCount = 3, params int[] segmentQuestionCounts)
    {
        if (segmentQuestionCounts.Length == 0)
        {
            segmentQuestionCounts = [3];
        }

        await ResetScoringDefaultsAsync();
        await SeedMcqAsync(segmentQuestionCounts.Sum());
        var stage = await CreateStageAsync(segmentQuestionCounts.Select(c => ("Mcq", c)).ToArray());
        var teams = await CreateTeamsAsync(teamCount);
        return await CreateMatchAsync(stage.Id, 1, teams);
    }

    /// <summary>A ProblemDetails body's errorCode extension (GlobalExceptionHandler).</summary>
    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    /// <summary>Adds whatever <paramref name="seed"/> builds (approved
    /// questions of any format, their items, …) in one save.</summary>
    public async Task SeedAsync(Action<AppDbContext, Guid> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db, ProgramId);
        await db.SaveChangesAsync();
    }

    public async Task<T> ReadDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private int _nextOrderIndex;

    private int NextOrderIndex() => _nextOrderIndex++;

    private static async Task<HttpClient> CreateSuperAdminClientAsync(CustomWebApplicationFactory factory)
    {
        var email = $"match-admin-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Test Admin", IsActive = true, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            await userManager.AddToRolesAsync(user, [Roles.SuperAdmin]);
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static async Task<HttpClient> CreateScopedClientAsync(
        CustomWebApplicationFactory factory, Guid programId, params string[] roles)
    {
        var email = $"match-user-{Guid.NewGuid():N}@quizapp.test";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true, EmailConfirmed = true };
        (await userManager.CreateAsync(user, "P@ssw0rd123!")).Succeeded.Should().BeTrue();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", tokenService.GenerateAccessToken(user, roles, programId));
        return client;
    }
}
