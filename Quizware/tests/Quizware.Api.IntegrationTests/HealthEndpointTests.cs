using System.Net;
using FluentAssertions;

namespace Quizware.Api.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk()
    {
        // Predicate = true so this one actually runs the DB check —
        // proving the migrated-and-seeded SQLite database is reachable.
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/admin/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
