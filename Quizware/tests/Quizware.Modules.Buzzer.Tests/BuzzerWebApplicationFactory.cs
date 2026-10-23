using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quizware.Api.IntegrationTests;
using Quizware.Application.Buzzer;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>The API on SQLite with the buzzer module installed, a fake
/// provider in place of hardware, and a known agent key.</summary>
public sealed class BuzzerWebApplicationFactory : CustomWebApplicationFactory
{
    public const string AgentKey = "test-agent-key-0123456789";

    public FakeBuzzerProvider Provider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Buzzer:Agent:ApiKey", AgentKey);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBuzzerProvider>();
            services.AddSingleton<IBuzzerProvider>(Provider);
        });
    }

    public HttpClient CreateAgentClient(string? key = AgentKey)
    {
        var client = CreateClient();
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add("X-Buzzer-Agent-Key", key);
        }

        return client;
    }
}
