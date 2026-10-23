using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Buzzer;

public sealed record BuzzerCapabilityDto(bool Available, string Provider, int DeviceCount);

public sealed record GetBuzzerCapabilityQuery : IRequest<BuzzerCapabilityDto>;

public sealed record GetBuzzerHealthQuery : IRequest<BuzzerHealth>;

/// <summary>Reads every button on every configured device — the rehearsal screen.</summary>
public sealed record TestBuzzersCommand : IRequest<IReadOnlyList<BuzzDeviceReading>>;

public sealed record SetDeviceMappingsCommand(Guid MatchId, IReadOnlyList<DeviceMappingInput> Mappings) : IRequest;

public sealed record ArmBuzzSessionCommand(Guid MatchId, Guid SegmentId, Guid MatchQuestionId) : IRequest<BuzzSessionDto>;

public sealed record GetBuzzSessionQuery(Guid SessionId) : IRequest<BuzzSessionDto>;

public sealed record SubmitBuzzPressCommand(Guid SessionId, int DeviceId, long ElapsedMs, string? RawFrameHex) : IRequest<BuzzSessionDto>;

public sealed record CollectBuzzSessionCommand(Guid SessionId) : IRequest<BuzzSessionDto>;

public sealed record ResetBuzzSessionCommand(Guid SessionId, string? Reason) : IRequest;

public sealed partial class SetDeviceMappingsCommandValidator : AbstractValidator<SetDeviceMappingsCommand>
{
    public SetDeviceMappingsCommandValidator()
    {
        RuleFor(x => x.Mappings).NotNull();
        RuleForEach(x => x.Mappings).ChildRules(m => m.RuleFor(x => x.DeviceId).GreaterThanOrEqualTo(1));
        RuleFor(x => x.Mappings)
            .Must(m => m.Select(x => x.DeviceId).Distinct().Count() == m.Count)
            .When(x => x.Mappings is not null)
            .WithMessage("Each device can be mapped only once.");
        RuleFor(x => x.Mappings)
            .Must(m => m.Select(x => x.MatchParticipantId).Distinct().Count() == m.Count)
            .When(x => x.Mappings is not null)
            .WithMessage("Each participant can have only one device.");
    }
}

public sealed partial class SubmitBuzzPressCommandValidator : AbstractValidator<SubmitBuzzPressCommand>
{
    public SubmitBuzzPressCommandValidator()
    {
        RuleFor(x => x.DeviceId).GreaterThanOrEqualTo(1);
        RuleFor(x => x.ElapsedMs).InclusiveBetween(0, int.MaxValue);
        RuleFor(x => x.RawFrameHex)
            .Must(h => h is null || (h.Length <= 128 && h.Length % 2 == 0 && HexPattern().IsMatch(h)))
            .WithMessage("RawFrameHex must be an even-length hex string of at most 64 bytes.");
    }

    [GeneratedRegex("^[0-9A-Fa-f]*$")]
    private static partial Regex HexPattern();
}

public sealed class BuzzerRequestsHandler :
    IRequestHandler<GetBuzzerCapabilityQuery, BuzzerCapabilityDto>,
    IRequestHandler<GetBuzzerHealthQuery, BuzzerHealth>,
    IRequestHandler<TestBuzzersCommand, IReadOnlyList<BuzzDeviceReading>>,
    IRequestHandler<SetDeviceMappingsCommand>,
    IRequestHandler<ArmBuzzSessionCommand, BuzzSessionDto>,
    IRequestHandler<GetBuzzSessionQuery, BuzzSessionDto>,
    IRequestHandler<SubmitBuzzPressCommand, BuzzSessionDto>,
    IRequestHandler<CollectBuzzSessionCommand, BuzzSessionDto>,
    IRequestHandler<ResetBuzzSessionCommand>
{
    private readonly IBuzzerProvider _provider;
    private readonly IBuzzerSessionService _sessions;

    public BuzzerRequestsHandler(IBuzzerProvider provider, IBuzzerSessionService sessions)
    {
        _provider = provider;
        _sessions = sessions;
    }

    public Task<BuzzerCapabilityDto> Handle(GetBuzzerCapabilityQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new BuzzerCapabilityDto(_provider.IsAvailable, _provider.Name, _provider.DeviceCount));

    /// <summary>Always answers: a provider that cannot even be asked is reported unhealthy, not as an error.</summary>
    public async Task<BuzzerHealth> Handle(GetBuzzerHealthQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _provider.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BuzzerHealth(false, ex.Message, []);
        }
    }

    public async Task<IReadOnlyList<BuzzDeviceReading>> Handle(TestBuzzersCommand request, CancellationToken cancellationToken)
    {
        if (!_provider.IsAvailable)
        {
            throw new BuzzerUnavailableException($"No buzzer is available (provider: {_provider.Name}).");
        }

        var devices = Enumerable.Range(1, Math.Max(_provider.DeviceCount, 1)).ToList();
        return await BuzzerCalls.GuardAsync(() => _provider.TestAsync(devices, cancellationToken));
    }

    public Task Handle(SetDeviceMappingsCommand request, CancellationToken cancellationToken) =>
        _sessions.SetMappingsAsync(request.MatchId, request.Mappings, cancellationToken);

    public Task<BuzzSessionDto> Handle(ArmBuzzSessionCommand request, CancellationToken cancellationToken) =>
        _sessions.ArmAsync(request.MatchId, request.SegmentId, request.MatchQuestionId, cancellationToken);

    public Task<BuzzSessionDto> Handle(GetBuzzSessionQuery request, CancellationToken cancellationToken) =>
        _sessions.GetAsync(request.SessionId, cancellationToken);

    public Task<BuzzSessionDto> Handle(SubmitBuzzPressCommand request, CancellationToken cancellationToken) =>
        _sessions.SubmitPressAsync(request.SessionId, request.DeviceId, request.ElapsedMs, request.RawFrameHex, cancellationToken);

    public Task<BuzzSessionDto> Handle(CollectBuzzSessionCommand request, CancellationToken cancellationToken) =>
        _sessions.CollectAsync(request.SessionId, cancellationToken);

    public Task Handle(ResetBuzzSessionCommand request, CancellationToken cancellationToken) =>
        _sessions.ResetAsync(request.SessionId, request.Reason, cancellationToken);
}

/// <summary>Turns any hardware or transport failure into a 503 — a buzzer
/// fault must never surface as a 500 (QuickBuzz audit, defect #4).</summary>
public static class BuzzerCalls
{
    public static async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is not (BuzzerUnavailableException or OperationCanceledException))
        {
            throw new BuzzerUnavailableException($"The buzzer did not respond: {ex.Message}", ex);
        }
    }

    public static Task GuardAsync(Func<Task> call) =>
        GuardAsync(async () =>
        {
            await call();
            return true;
        });
}
