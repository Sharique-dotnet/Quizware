using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Reports;
using Quizware.Api.Contracts.V1.Teams;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P13-05: the audit log query — filterable by entity, actor and date,
/// paged, scoped to the program, and open to the Auditor role.</summary>
public class AuditReportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuditReportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<AuditReportResponse> AuditAsync(HttpClient client, MatchTestHarness h, string query)
    {
        var response = await client.GetAsync($"/api/v1/programs/{h.ProgramId}/reports/audit?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuditReportResponse>())!;
    }

    [Fact]
    public async Task Audit_FiltersByEntityActorAndDate_AndPages()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var other = await MatchTestHarness.CreateAsync(_factory);
        var started = DateTime.UtcNow.AddSeconds(-1);
        var teamA = await h.CreateTeamAsync("Audit Alpha");
        var secondAdmin = await h.CreateClientAsync(Roles.ProgramAdmin);
        var created = await secondAdmin.PostAsJsonAsync(
            $"/api/v1/programs/{h.ProgramId}/teams", new CreateTeamRequest($"T{Guid.NewGuid():N}"[..10], "Beta School", "Audit Beta", ["Member A"]));
        created.EnsureSuccessStatusCode();
        var teamB = (await created.Content.ReadFromJsonAsync<TeamDetailResponse>())!.Id;
        var theirTeam = await other.CreateTeamAsync("Not Mine");

        var teams = await AuditAsync(h.Client, h, "entityName=Team");
        var alpha = await AuditAsync(h.Client, h, $"entityName=Team&entityId={teamA}");
        var betaRow = teams.Entries.Single(e => e.EntityId == teamB.ToString());
        var byActorId = await AuditAsync(h.Client, h, $"actor={betaRow.ActorUserId}");
        var byActorName = await AuditAsync(h.Client, h, $"actor={Uri.EscapeDataString(betaRow.ActorName![..20])}");
        var inserts = await AuditAsync(h.Client, h, "entityName=Team&action=Insert");
        var inWindow = await AuditAsync(h.Client, h, $"entityName=Team&from={started:O}&to={DateTime.UtcNow.AddMinutes(1):O}");
        var future = await AuditAsync(h.Client, h, $"from={DateTime.UtcNow.AddDays(1):O}");
        var firstPage = await AuditAsync(h.Client, h, "entityName=Team&pageSize=1");
        var secondPage = await AuditAsync(h.Client, h, "entityName=Team&pageSize=1&page=2");

        teams.Entries.Select(e => e.EntityId).Should().Contain([teamA.ToString(), teamB.ToString()])
            .And.NotContain(theirTeam.ToString(), "another program's audit rows are never returned");
        teams.Entries.Select(e => e.OccurredAtUtc).Should().BeInDescendingOrder();
        alpha.Entries.Should().NotBeEmpty().And.OnlyContain(e => e.EntityId == teamA.ToString());
        alpha.Entries.Should().Contain(e => e.Action == "Insert" && e.NewValuesJson!.Contains("Audit Alpha") && e.OldValuesJson == null);
        betaRow.ActorUserId.Should().NotBeEmpty();
        byActorId.Entries.Should().NotBeEmpty().And.OnlyContain(e => e.ActorUserId == betaRow.ActorUserId);
        byActorId.Entries.Select(e => e.EntityId).Should().Contain(teamB.ToString()).And.NotContain(teamA.ToString());
        byActorName.Entries.Select(e => e.Id).Should().BeEquivalentTo(byActorId.Entries.Select(e => e.Id));
        inserts.Entries.Should().OnlyContain(e => e.Action == "Insert");
        inWindow.Entries.Select(e => e.EntityId).Should().Contain([teamA.ToString(), teamB.ToString()]);
        future.Entries.Should().BeEmpty();
        future.TotalCount.Should().Be(0);
        firstPage.Entries.Should().ContainSingle();
        firstPage.TotalCount.Should().Be(teams.TotalCount).And.BeGreaterThanOrEqualTo(2);
        secondPage.Entries.Single().Id.Should().NotBe(firstPage.Entries.Single().Id);
    }

    [Fact]
    public async Task Audit_IsOpenToAuditors_ClosedToOperators_AndValidatesItsFilters()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.CreateTeamAsync("Audited");
        var auditor = await h.CreateClientAsync(Roles.Auditor);
        var operatorClient = await h.CreateClientAsync(Roles.Operator);
        var url = $"/api/v1/programs/{h.ProgramId}/reports/audit";

        var asAuditor = await auditor.GetAsync(url);
        var reportAsAuditor = await auditor.GetAsync($"/api/v1/programs/{h.ProgramId}/reports/questions/usage");
        var asOperator = await operatorClient.GetAsync(url);
        var badPageSize = await h.Client.GetAsync($"{url}?pageSize=0");
        var badAction = await h.Client.GetAsync($"{url}?action=Drop");
        var backwards = await h.Client.GetAsync($"{url}?from=2026-02-01&to=2026-01-01");

        asAuditor.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asAuditor.Content.ReadFromJsonAsync<AuditReportResponse>())!.Entries.Should().NotBeEmpty();
        reportAsAuditor.StatusCode.Should().Be(HttpStatusCode.OK, "the Auditor has read-only access to every report");
        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        badPageSize.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badAction.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        backwards.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
