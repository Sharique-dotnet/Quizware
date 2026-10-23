using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quizware.Api.IntegrationTests;
using Quizware.Modules.Buzzer.Abstractions;
using Quizware.Modules.Buzzer.Agent;
using Quizware.Modules.Buzzer.Serial;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>The API with <c>Buzzer:Provider = HttpAgent</c>, plus a real
/// <see cref="BuzzerAgentRunner"/> wired to it over HTTP — the agent as it
/// runs on the operator PC, with only the COM port faked.</summary>
public sealed class HttpAgentWebApplicationFactory : CustomWebApplicationFactory
{
    public const string AgentKey = "agent-key-for-http-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Buzzer:Provider", "HttpAgent");
        builder.UseSetting("Buzzer:DeviceCount", "3");
        builder.UseSetting("Buzzer:Agent:ApiKey", AgentKey);
        builder.UseSetting("Buzzer:Agent:PollIntervalMs", "50");
    }

    public (BuzzerAgentRunner Runner, FakeSerialPort Port) CreateAgent(string key = AgentKey)
    {
        var port = new FakeSerialPort();
        var options = Options.Create(new BuzzerOptions
        {
            DeviceCount = 3,
            Serial = new SerialOptions { BusDelayMs = 0, RetryCount = 1 },
        });
        var http = CreateClient();
        http.DefaultRequestHeaders.Add(HttpBuzzerAgentApi.AgentKeyHeader, key);
        var runner = new BuzzerAgentRunner(
            new SerialDeviceBus(port, options, NullLogger<SerialDeviceBus>.Instance),
            new HttpBuzzerAgentApi(http),
            options,
            NullLogger<BuzzerAgentRunner>.Instance) { AgentId = "test-agent" };
        return (runner, port);
    }
}
