using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Api.Contracts.V1.Rules;
using Quizware.Api.Contracts.V1.Scores;
using Quizware.Application.Authorization;
using Xunit.Abstractions;

namespace Quizware.Api.IntegrationTests;

/// <summary>P15-03, the tenant-isolation penetration test: a user of program
/// A, holding every program-scoped role, forges program B's ids into every
/// route the API exposes (read from the running app, so a new endpoint is
/// covered the day it is added) and into the bodies of the writes that take
/// foreign keys. Nothing may succeed, nothing may crash, nothing of B's may
/// appear in a response, and B's data must be untouched afterwards.</summary>
public partial class TenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly string[] ProgramRoles =
        [Roles.ProgramAdmin, Roles.Operator, Roles.QuestionAuthor, Roles.Scorer, Roles.Auditor];

    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public TenantIsolationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>Program B, mid-match, with a canary name on its private data.</summary>
    private sealed record Victim(
        MatchTestHarness H,
        Guid MatchId,
        Guid StageId,
        Guid TeamId,
        Guid TopicId,
        Guid QuestionId,
        Guid MatchSegmentId,
        Guid StageSegmentId,
        Guid MatchQuestionId,
        Guid AnswerId,
        Guid ParticipantId,
        string Canary);

    private async Task<Victim> VictimAsync()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var canary = $"Canary{Guid.NewGuid():N}"[..20];
        var team = await h.CreateTeamAsync(canary);
        var topic = await h.SeedTopicAsync(canary + "-topic");
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var answer = (await (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId;
        await h.ServeAsync(started);

        var ids = await h.ReadDbAsync(async db => new
        {
            Stage = started.Match.StageId,
            StageSegment = await db.StageSegmentTemplates.Where(t => t.StageId == started.Match.StageId).Select(t => t.Id).FirstAsync(),
            Question = await db.MatchQuestions.Where(q => q.Id == question.MatchQuestionId).Select(q => q.QuestionId).SingleAsync(),
        });

        return new Victim(
            h, started.Match.Id, ids.Stage, team, topic, ids.Question, started.Segments[0].Id, ids.StageSegment,
            question.MatchQuestionId, answer, holder, canary);
    }

    private sealed record Probe(string Method, string Url, HttpStatusCode Status, string Body);

    /// <summary>Every routed API action with at least one id in its route.</summary>
    private IReadOnlyList<(string Method, string Template)> Routes() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw && raw.StartsWith("api/v1", StringComparison.Ordinal) && raw.Contains('{'))
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (m, "/" + e.RoutePattern.RawText!)))
            .Distinct()
            .OrderBy(r => r.Item2)
            .ToList();

    [GeneratedRegex(@"\{(\w+)(:[^}]*)?\}")]
    private static partial Regex RouteParameter();

    /// <summary>Fills a route with B's ids. <paramref name="programId"/> is the
    /// program put in the route: B's (forged program) or A's (forged entity ids).</summary>
    private static string Fill(string template, Victim b, Guid programId)
    {
        var url = RouteParameter().Replace(template, m =>
        {
            var before = template[..m.Index];
            return m.Groups[1].Value switch
            {
                "programId" => programId.ToString(),
                "id" when before.EndsWith("/programs/", StringComparison.Ordinal) => programId.ToString(),
                "id" when before.EndsWith("/matches/", StringComparison.Ordinal) => b.MatchId.ToString(),
                "id" when before.EndsWith("/stages/", StringComparison.Ordinal) => b.StageId.ToString(),
                "id" when before.EndsWith("/teams/", StringComparison.Ordinal) => b.TeamId.ToString(),
                "id" when before.EndsWith("/topics/", StringComparison.Ordinal) => b.TopicId.ToString(),
                "id" when before.Contains("/questions/", StringComparison.Ordinal) => b.QuestionId.ToString(),
                "id" when before.EndsWith("/answers/", StringComparison.Ordinal) => b.AnswerId.ToString(),
                "matchId" => b.MatchId.ToString(),
                "stageId" => b.StageId.ToString(),
                "teamId" => b.TeamId.ToString(),
                "segId" when before.Contains("/matches/", StringComparison.Ordinal) || before.Contains("/live", StringComparison.Ordinal)
                    => b.MatchSegmentId.ToString(),
                "segId" => b.StageSegmentId.ToString(),
                "mqId" => b.MatchQuestionId.ToString(),
                "pid" => b.ParticipantId.ToString(),
                "formatCode" => "mcq",
                "reportType" => "match",
                _ => Guid.NewGuid().ToString(),
            };
        });

        return url.Contains("/reports/export/", StringComparison.Ordinal) ? $"{url}?id={b.MatchId}&format=csv" : url;
    }

    private static async Task<Probe> SendAsync(HttpClient client, string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        return new Probe(method, url, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A refusal is any 4xx, a 501 stub, or the buzzer's 503 — never
    /// a success, never a crash, and never a response that echoes B's data.</summary>
    private static string? Violation(Probe p, Victim b)
    {
        var status = (int)p.Status;
        if (status is >= 200 and < 300)
        {
            return "succeeded";
        }

        if (status >= 500 && !(status == 501 || (status == 503 && p.Body.Contains("BUZZER_UNAVAILABLE", StringComparison.Ordinal))))
        {
            return "crashed";
        }

        return p.Body.Contains(b.Canary, StringComparison.Ordinal) ? "leaked B's data" : null;
    }

    private async Task<(Guid Matches, int ScoreEvents, int Answers, int Teams, string MatchState)> SnapshotAsync(Victim b) =>
        await b.H.ReadDbAsync(async db => (
            b.MatchId,
            await db.ScoreEvents.CountAsync(e => e.MatchId == b.MatchId),
            await db.AnswerRecords.CountAsync(a => a.MatchId == b.MatchId && !a.IsReversed),
            await db.Teams.CountAsync(t => t.ProgramId == b.H.ProgramId),
            (await db.Matches.SingleAsync(m => m.Id == b.MatchId)).State.ToString()));

    [Fact]
    public async Task EveryRoute_RefusesAnotherProgramsIds_InTheRouteItself()
    {
        var b = await VictimAsync();
        var a = await MatchTestHarness.CreateAsync(_factory);
        var attacker = await a.CreateClientAsync(ProgramRoles);
        var before = await SnapshotAsync(b);
        var routes = Routes();

        var probes = new List<Probe>();

        // Forged program: B's program id (and B's entity ids) on A's token.
        foreach (var (method, template) in routes.Where(r => r.Template.Contains("{programId", StringComparison.Ordinal)
            || r.Template.Contains("/programs/{id", StringComparison.Ordinal)))
        {
            probes.Add(await SendAsync(attacker, method, Fill(template, b, b.H.ProgramId)));
        }

        // Forged entities: A's own program, every other id one of B's. Reads go
        // first so a write that wrongly succeeded cannot hide a read leak.
        foreach (var (method, template) in routes
            .Where(r => !r.Template.EndsWith("/programs/{id:guid}", StringComparison.Ordinal) || r.Method != "GET")
            .OrderBy(r => r.Method == "GET" ? 0 : 1))
        {
            var url = Fill(template, b, a.ProgramId);
            if (url.Contains($"/programs/{a.ProgramId}", StringComparison.Ordinal) && !RouteParameter().Matches(template)
                .Any(m => m.Groups[1].Value is not ("programId" or "formatCode")
                    && !(m.Groups[1].Value == "id" && template[..m.Index].EndsWith("/programs/", StringComparison.Ordinal))))
            {
                continue; // only A's own ids in this route: nothing of B's to forge
            }

            probes.Add(await SendAsync(attacker, method, url));
        }

        var violations = probes.Select(p => (p, Why: Violation(p, b))).Where(x => x.Why is not null).ToList();
        _output.WriteLine($"{probes.Count} forged requests over {routes.Count} routes; {violations.Count} violations.");
        foreach (var (p, why) in violations)
        {
            _output.WriteLine($"{why}: {p.Method} {p.Url} → {(int)p.Status} {p.Body[..Math.Min(p.Body.Length, 200)]}");
        }

        probes.Count.Should().BeGreaterThan(150);
        violations.Select(v => $"{v.Why}: {v.p.Method} {v.p.Url} → {(int)v.p.Status}").Should().BeEmpty();
        (await SnapshotAsync(b)).Should().Be(before, "program B's data is untouched");
    }

    [Fact]
    public async Task Writes_RefuseAnotherProgramsIds_InTheBody()
    {
        var b = await VictimAsync();
        var a = await MatchTestHarness.CreateAsync(_factory);
        var started = await a.StartWithFirstSegmentOpenAsync(await a.CreateReadyToStartMatchAsync(2, 2));
        var question = await a.ServeAsync(started);
        var draft = await a.CreateReadyToStartMatchAsync(2, 2);
        var attacker = await a.CreateClientAsync(ProgramRoles);
        var programs = $"/api/v1/programs/{a.ProgramId}";
        (await attacker.PutAsJsonAsync($"{programs}/rules/qualification", new UpsertQualificationRulesRequest(
            [new QualificationRuleDto(Guid.Empty, started.Match.StageId, 1, 0, 1)]))).EnsureSuccessStatusCode();
        var before = await SnapshotAsync(b);

        var forged = new (string What, Func<Task<HttpResponseMessage>> Send)[]
        {
            ("answer for B's participant", () => attacker.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
                question.MatchQuestionId, b.ParticipantId, "Correct", null, null, null, 0, "Operator", null, null))),
            ("answer on B's question", async () => await attacker.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
                b.MatchQuestionId, (await a.StateAsync(started)).ActiveParticipant!.ParticipantId, "Correct", null, null, null, 0, "Operator", null, null))),
            ("B's team into A's match", () => attacker.PostAsJsonAsync(
                $"{programs}/matches/{draft.Id}/participants", new AddMatchParticipantRequest(b.TeamId, 3))),
            ("A's match in B's stage", () => attacker.PostAsJsonAsync(
                $"{programs}/matches", new CreateMatchRequest(b.StageId, "Forged", 99))),
            ("score adjustment for B's team", () => attacker.PostAsJsonAsync(
                $"/api/v1/matches/{started.Match.Id}/scores/adjust", new AdjustScoreRequest(b.TeamId, 50, "Forged"))),
            ("B's segments reordered in A's match", () => attacker.PutAsJsonAsync(
                $"{programs}/matches/{draft.Id}/segments/reorder", new ReorderMatchSegmentsRequest([b.MatchSegmentId], "Forged"))),
            ("B's team as A's wildcard", () => attacker.PostAsJsonAsync(
                $"{programs}/qualification/stages/{started.Match.StageId}/wildcards", new SetWildcardsRequest([b.TeamId]))),
            ("qualification rule on B's stage", () => attacker.PutAsJsonAsync(
                $"{programs}/rules/qualification", new UpsertQualificationRulesRequest([new QualificationRuleDto(Guid.Empty, b.StageId, 1, 1, 0)]))),
            ("tie-break rule on B's stage", () => attacker.PutAsJsonAsync(
                $"{programs}/rules/tie-break", new UpsertTieBreakRulesRequest([new TieBreakRuleDto(
                    Guid.Empty, b.StageId, ["TotalScore"], "Mcq", 2, false, 3, false, "ManualDecision")]))),
            ("selection rule on B's stage", () => attacker.PutAsJsonAsync(
                $"{programs}/rules/selection", new UpsertSelectionRulesRequest([new SelectionRuleDto(
                    Guid.Empty, "Mcq", b.StageId, null, "NeverInProgram", "None", "WidenThenFail")]))),
            ("buzzer device for B's participant", () => attacker.PutAsJsonAsync(
                $"/api/v1/buzzer/matches/{started.Match.Id}/mappings", new SetDeviceMappingsRequest([new DeviceMappingEntry(1, b.ParticipantId)]))),
        };

        var results = new List<string>();
        foreach (var (what, send) in forged)
        {
            using var response = await send();
            var body = await response.Content.ReadAsStringAsync();
            var probe = new Probe("BODY", what, response.StatusCode, body);
            var why = Violation(probe, b);
            _output.WriteLine($"{what}: {(int)response.StatusCode} {body[..Math.Min(body.Length, 160)]}");
            if (why is not null)
            {
                results.Add($"{why}: {what} → {(int)response.StatusCode}");
            }
        }

        results.Should().BeEmpty();
        (await SnapshotAsync(b)).Should().Be(before, "program B's data is untouched");
        (await a.ReadDbAsync(db => db.Matches.CountAsync(m => m.StageId == b.StageId && m.ProgramId == a.ProgramId))).Should().Be(0);
    }

    [Fact]
    public async Task ProgramManagement_StaysOpenToAnUnscopedSuperAdmin_ButNotToAnotherProgramsAdmin()
    {
        var a = await MatchTestHarness.CreateAsync(_factory);
        var b = await MatchTestHarness.CreateAsync(_factory);
        var superAdmin = await MatchTestHarness.CreateUnscopedSuperAdminClientAsync(_factory);
        var adminOfB = await b.CreateClientAsync(Roles.ProgramAdmin);

        var asSuperAdmin = await superAdmin.GetAsync($"/api/v1/programs/{a.ProgramId}");
        var asOtherAdmin = await adminOfB.GetAsync($"/api/v1/programs/{a.ProgramId}/settings");
        var asOwnAdmin = await (await a.CreateClientAsync(Roles.ProgramAdmin)).GetAsync($"/api/v1/programs/{a.ProgramId}/settings");

        asSuperAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
        asOtherAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        asOwnAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
