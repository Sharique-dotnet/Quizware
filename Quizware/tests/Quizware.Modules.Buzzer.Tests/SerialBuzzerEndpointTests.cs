using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Api.IntegrationTests;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>P14-04/07 through the API: the serial provider selected by
/// configuration arms the devices, reads the presses on collect, and serves
/// the rehearsal screen.</summary>
public class SerialBuzzerEndpointTests : IClassFixture<SerialBuzzerWebApplicationFactory>
{
    private readonly SerialBuzzerWebApplicationFactory _factory;

    public SerialBuzzerEndpointTests(SerialBuzzerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ArmAndCollect_ReadThePressesFromTheBus_AndRankThem()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var participants = (await h.StateAsync(started)).Participants.OrderBy(p => p.SeatNumber).Select(p => p.ParticipantId).ToList();
        (await h.Client.PutAsJsonAsync($"/api/v1/buzzer/matches/{started.Match.Id}/mappings", new SetDeviceMappingsRequest(
            [new(1, participants[0]), new(2, participants[1]), new(3, participants[2])]))).EnsureSuccessStatusCode();
        foreach (var device in new[] { 1, 2, 3 })
        {
            _factory.Port.Devices[device] = new int[5];
        }

        var capability = await h.Client.GetFromJsonAsync<BuzzerCapabilityResponse>("/api/v1/buzzer/capability");
        var arm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(started.Segments[0].Id, question.MatchQuestionId));
        arm.StatusCode.Should().Be(HttpStatusCode.OK, await arm.Content.ReadAsStringAsync());
        var sessionId = (await arm.Content.ReadFromJsonAsync<ArmBuzzSessionResponse>())!.SessionId;

        // The teams press after arming: device 3 first, then device 1; device 2 never.
        _factory.Port.Devices[3][4] = 180;
        _factory.Port.Devices[1][0] = 260;
        var collect = await h.Client.PostAsync($"/api/v1/buzzer/sessions/{sessionId}/collect", null);
        var session = (await collect.Content.ReadFromJsonAsync<BuzzSessionResponse>())!;
        var test = await h.Client.PostAsync("/api/v1/buzzer/test", null);

        capability!.Should().Be(new BuzzerCapabilityResponse(true, "Serial", 3));
        collect.StatusCode.Should().Be(HttpStatusCode.OK);
        session.Provider.Should().Be("Serial");
        session.RankedPresses.Select(p => (p.DeviceId, p.ButtonKey, p.ElapsedMs, p.Rank, p.MatchParticipantId))
            .Should().Equal((3, "E", 180L, 1, participants[2]), (1, "A", 260L, 2, participants[0]));
        session.RankedPresses.Should().OnlyContain(p => p.RawFrameHex!.Length == 48);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        (await test.Content.ReadFromJsonAsync<BuzzerTestResponse>())!.Devices.Should().HaveCount(3)
            .And.Contain(d => d.DeviceId == 3 && d.ButtonTimesMs!["E"] == 180);
    }
}
