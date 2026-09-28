using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9a: match setup — CRUD, participants, per-match segments,
/// and the ready check.</summary>
public class MatchesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MatchesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<MatchSegmentsResponse> GetSegmentsAsync(MatchTestHarness h, Guid matchId) =>
        (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{h.MatchesUrl}/{matchId}/segments"))!;

    [Fact]
    public async Task Create_InstantiatesSegmentsFromStageTemplatesInOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2));

        var response = await h.Client.PostAsJsonAsync(h.MatchesUrl, new CreateMatchRequest(stage.Id, "Opener", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var match = (await response.Content.ReadFromJsonAsync<MatchDetailResponse>())!;
        match.Name.Should().Be("Opener");
        match.State.Should().Be("Draft");
        match.MatchKind.Should().Be("Regular");

        var segments = await GetSegmentsAsync(h, match.Id);
        segments.Segments.Select(s => s.FormatCode).Should().Equal("Mcq", "Buzzer");
        segments.Segments.Select(s => s.OrderIndex).Should().Equal(0, 1);
        segments.Segments.Should().OnlyContain(s => s.State == "Pending");
    }

    [Fact]
    public async Task Create_DuplicateMatchNumberInStage_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        await h.CreateMatchAsync(stage.Id, 1, []);

        var response = await h.Client.PostAsJsonAsync(h.MatchesUrl, new CreateMatchRequest(stage.Id, "Again", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_AsOperator_IsForbidden()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var operatorClient = await h.CreateClientAsync(Roles.Operator);

        var response = await operatorClient.PostAsJsonAsync(h.MatchesUrl, new CreateMatchRequest(stage.Id, "X", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_FiltersByStage_AndRejectsUnknownState()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stageA = await h.CreateStageAsync(("Mcq", 3));
        var stageB = await h.CreateStageAsync(("Mcq", 3));
        await h.CreateMatchAsync(stageA.Id, 1, []);
        await h.CreateMatchAsync(stageB.Id, 1, []);

        var filtered = await h.Client.GetFromJsonAsync<List<MatchSummaryResponse>>($"{h.MatchesUrl}?stageId={stageA.Id}");
        var badState = await h.Client.GetAsync($"{h.MatchesUrl}?state=Bogus");

        filtered!.Should().ContainSingle().Which.StageId.Should().Be(stageA.Id);
        badState.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ChangesNameAndNumber()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);

        var response = await h.Client.PutAsJsonAsync($"{h.MatchesUrl}/{match.Id}", new UpdateMatchRequest("Renamed", 7));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<MatchDetailResponse>())!;
        updated.Name.Should().Be("Renamed");
        updated.MatchNumber.Should().Be(7);
    }

    [Fact]
    public async Task Delete_UnstartedMatch_RemovesIt()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));

        var delete = await h.Client.DeleteAsync($"{h.MatchesUrl}/{match.Id}");
        var get = await h.Client.GetAsync($"{h.MatchesUrl}/{match.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddParticipant_AssignsNextTurnOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var teams = await h.CreateTeamsAsync(2);

        var match = await h.CreateMatchAsync(stage.Id, 1, teams);

        match.Participants.Select(p => p.TurnOrder).Should().Equal(1, 2);
        match.Participants.Select(p => p.SeatNumber).Should().Equal(1, 2);
        match.Participants.Should().OnlyContain(p => p.Status == "Active" && p.TeamName.StartsWith("Team "));
    }

    [Fact]
    public async Task AddParticipant_SeatTaken_Or_TeamAlreadyIn_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var teams = await h.CreateTeamsAsync(2);
        var match = await h.CreateMatchAsync(stage.Id, 1, [teams[0]]);

        var seatTaken = await h.Client.PostAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(teams[1], 1));
        var teamAlreadyIn = await h.Client.PostAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(teams[0], 5));

        seatTaken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        teamAlreadyIn.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddParticipant_BeyondStageMaximum_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var teams = await h.CreateTeamsAsync(4);
        var match = await h.CreateMatchAsync(stage.Id, 1, teams.Take(3).ToList());

        var response = await h.Client.PostAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(teams[3], 4));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveParticipant_RecompactsTurnOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(3));
        var first = match.Participants.Single(p => p.TurnOrder == 1);

        var response = await h.Client.DeleteAsync($"{h.MatchesUrl}/{match.Id}/participants/{first.Id}");
        var after = await h.Client.GetFromJsonAsync<MatchDetailResponse>($"{h.MatchesUrl}/{match.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Participants.Should().HaveCount(2);
        after.Participants.Select(p => p.TurnOrder).Order().Should().Equal(1, 2);
    }

    [Fact]
    public async Task OrderParticipants_SwapSeats_Succeeds()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));
        var (a, b) = (match.Participants[0], match.Participants[1]);

        var response = await h.Client.PutAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants/order",
            new OrderMatchParticipantsRequest(
            [
                new MatchParticipantOrderEntry(a.Id, b.SeatNumber, 2),
                new MatchParticipantOrderEntry(b.Id, a.SeatNumber, 1),
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ordered = (await response.Content.ReadFromJsonAsync<List<MatchParticipantDto>>())!;
        ordered.Single(p => p.Id == a.Id).SeatNumber.Should().Be(b.SeatNumber);
        ordered.Single(p => p.Id == b.Id).TurnOrder.Should().Be(1);
    }

    [Fact]
    public async Task OrderParticipants_TurnOrderWithGap_ReturnsBadRequest()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));

        var response = await h.Client.PutAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants/order",
            new OrderMatchParticipantsRequest(
            [
                new MatchParticipantOrderEntry(match.Participants[0].Id, 1, 1),
                new MatchParticipantOrderEntry(match.Participants[1].Id, 2, 3),
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddSegment_AppendsAtEnd_AndRefusesFormatWithoutTemplate()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);

        var added = await h.Client.PostAsJsonAsync($"{h.MatchesUrl}/{match.Id}/segments", new AddMatchSegmentRequest("Mcq", 2));
        var noTemplate = await h.Client.PostAsJsonAsync($"{h.MatchesUrl}/{match.Id}/segments", new AddMatchSegmentRequest("Card", 2));

        added.StatusCode.Should().Be(HttpStatusCode.OK);
        (await added.Content.ReadFromJsonAsync<MatchSegmentSummaryDto>())!.OrderIndex.Should().Be(1);
        noTemplate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveSegment_ClosesTheGap()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2), ("Card", 2));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);
        var before = await GetSegmentsAsync(h, match.Id);

        var response = await h.Client.DeleteAsync($"{h.MatchesUrl}/{match.Id}/segments/{before.Segments[0].Id}");
        var after = await GetSegmentsAsync(h, match.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.Segments.Select(s => s.FormatCode).Should().Equal("Buzzer", "Card");
        after.Segments.Select(s => s.OrderIndex).Should().Equal(0, 1);
    }

    [Fact]
    public async Task ReorderSegments_FullList_Succeeds()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2), ("Card", 2));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);
        var ids = (await GetSegmentsAsync(h, match.Id)).Segments.Select(s => s.Id).ToList();

        var response = await h.Client.PutAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/segments/reorder", new ReorderMatchSegmentsRequest([ids[2], ids[0], ids[1]], "Crowd request"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<MatchSegmentsResponse>())!;
        result.Segments.Select(s => s.FormatCode).Should().Equal("Card", "Mcq", "Buzzer");
    }

    [Fact]
    public async Task ReorderSegments_LockedSegmentKeepsItsSlot()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2), ("Card", 2));
        var templates = (await h.Client.GetFromJsonAsync<List<Contracts.V1.Stages.StageSegmentTemplateDto>>(
            $"/api/v1/programs/{h.ProgramId}/stages/{stage.Id}/segments"))!;
        await h.Client.PutAsJsonAsync(
            $"/api/v1/programs/{h.ProgramId}/stages/{stage.Id}/segments/{templates[0].Id}",
            new Contracts.V1.Stages.UpdateSegmentTemplateRequest(3, true));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);
        var ids = (await GetSegmentsAsync(h, match.Id)).Segments.Select(s => s.Id).ToList();

        var response = await h.Client.PutAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/segments/reorder", new ReorderMatchSegmentsRequest([ids[2], ids[1], ids[0]], "Try to move the lock"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<MatchSegmentsResponse>())!;
        result.Segments.Select(s => s.FormatCode).Should().Equal("Mcq", "Card", "Buzzer");
        result.Segments[0].IsOrderLocked.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderSegments_PartialList_ReturnsBadRequest()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);
        var ids = (await GetSegmentsAsync(h, match.Id)).Segments.Select(s => s.Id).ToList();

        var response = await h.Client.PutAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/segments/reorder", new ReorderMatchSegmentsRequest([ids[0]], "x"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetToTemplate_DiscardsPerMatchChanges()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 3), ("Buzzer", 2));
        var match = await h.CreateMatchAsync(stage.Id, 1, []);
        var ids = (await GetSegmentsAsync(h, match.Id)).Segments.Select(s => s.Id).ToList();
        await h.Client.DeleteAsync($"{h.MatchesUrl}/{match.Id}/segments/{ids[0]}");
        await h.Client.PostAsJsonAsync($"{h.MatchesUrl}/{match.Id}/segments", new AddMatchSegmentRequest("Mcq", 1));

        var response = await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/segments/reset-to-template", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<MatchSegmentsResponse>())!;
        result.Segments.Select(s => s.FormatCode).Should().Equal("Mcq", "Buzzer");
        (await GetSegmentsAsync(h, match.Id)).Segments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ready_WithGaps_ReportsEveryBlocker_AndStaysDraft()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 5));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(1));

        var response = await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/ready", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<MatchReadyResponse>())!;
        result.IsReady.Should().BeFalse();
        result.Blockers.Should().Contain(b => b.StartsWith("INSUFFICIENT_PARTICIPANTS"));
        result.Blockers.Should().Contain(b => b.StartsWith("QUESTION_POOL_EXHAUSTED"));
        result.Blockers.Should().Contain(b => b.StartsWith("SCORING_RULE_MISSING"));
        (await h.Client.GetFromJsonAsync<MatchDetailResponse>($"{h.MatchesUrl}/{match.Id}"))!.State.Should().Be("Draft");
    }

    [Fact]
    public async Task Ready_WhenSatisfied_MarksReady_AndASetupEditSendsItBackToDraft()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(teamCount: 2);

        var response = await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/ready", null);
        var result = (await response.Content.ReadFromJsonAsync<MatchReadyResponse>())!;
        var readyState = (await h.Client.GetFromJsonAsync<MatchDetailResponse>($"{h.MatchesUrl}/{match.Id}"))!.State;
        await h.Client.PostAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(await h.CreateTeamAsync("Late"), 9));
        var afterEdit = (await h.Client.GetFromJsonAsync<MatchDetailResponse>($"{h.MatchesUrl}/{match.Id}"))!.State;

        result.IsReady.Should().BeTrue(string.Join("; ", result.Blockers));
        readyState.Should().Be("Ready");
        afterEdit.Should().Be("Draft");
    }
}
