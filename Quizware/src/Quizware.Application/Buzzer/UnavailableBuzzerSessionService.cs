using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Buzzer;

/// <summary>Registered when the buzzer module is absent: every buzzer
/// operation is a 503, and the live state says the buzzer is unavailable.
/// Nothing else in the system is affected.</summary>
public sealed class UnavailableBuzzerSessionService : IBuzzerSessionService
{
    private const string Reason = "The buzzer module is not installed.";

    public Task SetMappingsAsync(Guid matchId, IReadOnlyList<DeviceMappingInput> mappings, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task<BuzzSessionDto> ArmAsync(Guid matchId, Guid segmentId, Guid matchQuestionId, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task<BuzzSessionDto> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task<BuzzSessionDto> SubmitPressAsync(
        Guid sessionId, int deviceId, long elapsedMs, string? rawFrameHex, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task<BuzzSessionDto> CollectAsync(Guid sessionId, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task ResetAsync(Guid sessionId, string? reason, CancellationToken cancellationToken) =>
        throw new BuzzerUnavailableException(Reason);

    public Task<LiveBuzzerStatusDto> LiveStatusAsync(Guid matchId, CancellationToken cancellationToken) =>
        Task.FromResult(new LiveBuzzerStatusDto(false, null, "Unavailable"));

    public Task<IReadOnlyList<ArmedBuzzSessionDto>> ArmedSessionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArmedBuzzSessionDto>>([]);
}
