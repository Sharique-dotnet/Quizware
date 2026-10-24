using FluentValidation;
using MediatR;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Buzzer;

public sealed record AgentDeviceReport(int DeviceId, bool IsOnline, IReadOnlyDictionary<string, int>? ButtonTimesMs, string? RawFrameHex);

public sealed record AgentHeartbeat(string AgentId, string? PortName, IReadOnlyList<AgentDeviceReport> Devices);

public sealed record AgentHeartbeatReplyDto(IReadOnlyList<ArmedBuzzSessionDto> ArmedSessions, int PollIntervalMs);

/// <summary>Where the buzzer agent's heartbeat lands (P14-06). Only the HTTP
/// agent provider accepts heartbeats; otherwise the agent endpoint is a 503.</summary>
public interface IBuzzerAgentLink
{
    bool AcceptsAgent { get; }

    /// <summary>How often the agent should call back, in milliseconds.</summary>
    int PollIntervalMs { get; }

    void RecordHeartbeat(AgentHeartbeat heartbeat);
}

public sealed class NoBuzzerAgentLink : IBuzzerAgentLink
{
    public bool AcceptsAgent => false;

    public int PollIntervalMs => 0;

    public void RecordHeartbeat(AgentHeartbeat heartbeat) =>
        throw new BuzzerUnavailableException("This server does not take a buzzer agent (Buzzer:Provider is not HttpAgent).");
}

/// <summary>The agent reports its devices and learns which sessions are
/// armed — the only way it hears about one, since nothing calls into the
/// operator PC (outbound push only).</summary>
public sealed record RecordAgentHeartbeatCommand(AgentHeartbeat Heartbeat) : IRequest<AgentHeartbeatReplyDto>;

public sealed class RecordAgentHeartbeatCommandValidator : AbstractValidator<RecordAgentHeartbeatCommand>
{
    public RecordAgentHeartbeatCommandValidator()
    {
        RuleFor(x => x.Heartbeat.AgentId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Heartbeat.Devices).NotNull();
        RuleForEach(x => x.Heartbeat.Devices).ChildRules(d => d.RuleFor(x => x.DeviceId).GreaterThanOrEqualTo(1));
    }
}

public sealed class RecordAgentHeartbeatCommandHandler : IRequestHandler<RecordAgentHeartbeatCommand, AgentHeartbeatReplyDto>
{
    private readonly IBuzzerAgentLink _link;
    private readonly IBuzzerSessionService _sessions;

    public RecordAgentHeartbeatCommandHandler(IBuzzerAgentLink link, IBuzzerSessionService sessions)
    {
        _link = link;
        _sessions = sessions;
    }

    public async Task<AgentHeartbeatReplyDto> Handle(RecordAgentHeartbeatCommand request, CancellationToken cancellationToken)
    {
        _link.RecordHeartbeat(request.Heartbeat);
        return new AgentHeartbeatReplyDto(await _sessions.ArmedSessionsAsync(cancellationToken), _link.PollIntervalMs);
    }
}
