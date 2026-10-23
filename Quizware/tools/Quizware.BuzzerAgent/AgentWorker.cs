using Microsoft.Extensions.Options;
using Quizware.Modules.Buzzer.Agent;

namespace Quizware.BuzzerAgent;

/// <summary>Runs <see cref="BuzzerAgentRunner"/> until the service stops. An
/// unreachable API or a serial fault is logged and retried — the agent never
/// exits on its own, and the show never depends on it.</summary>
public sealed class AgentWorker : BackgroundService
{
    private readonly BuzzerAgentRunner _runner;
    private readonly AgentHostOptions _options;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(BuzzerAgentRunner runner, IOptions<AgentHostOptions> options, ILogger<AgentWorker> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
        _runner.AgentId = _options.AgentId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("Agent:ApiKey is not set; it must match the API's Buzzer:Agent:ApiKey. The agent is idle.");
            return;
        }

        _logger.LogInformation("Buzzer agent {AgentId} pushing to {Api}", _options.AgentId, _options.ApiBaseUrl);
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                wait = await _runner.TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("The API at {Api} is not reachable ({Message}); retrying", _options.ApiBaseUrl, ex.Message);
                wait = TimeSpan.FromMilliseconds(_options.RetryDelayMs);
            }

            await Task.Delay(wait, stoppingToken);
        }
    }
}
