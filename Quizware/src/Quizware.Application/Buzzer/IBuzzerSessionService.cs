using Quizware.Application.Gameplay.Dtos;

namespace Quizware.Application.Buzzer;

public sealed record BuzzPressItemDto(
    Guid PressId,
    int DeviceId,
    Guid? MatchParticipantId,
    Guid? TeamId,
    string? TeamName,
    string ButtonKey,
    int ElapsedMs,
    int Rank,
    string? RawFrameHex,
    DateTime ReceivedAtUtc);

public sealed record BuzzSessionDto(
    Guid SessionId,
    Guid MatchId,
    Guid? MatchQuestionId,
    string State,
    string Provider,
    int WindowMs,
    string? FailureReason,
    DateTime? ArmedAtUtc,
    DateTime? CollectedAtUtc,
    IReadOnlyList<BuzzPressItemDto> RankedPresses);

public sealed record DeviceMappingInput(int DeviceId, Guid MatchParticipantId);

/// <summary>A session the HTTP agent should be polling for presses.</summary>
public sealed record ArmedBuzzSessionDto(Guid SessionId, Guid MatchId, IReadOnlyList<int> DeviceIds, int WindowMs, DateTime ArmedAtUtc);

/// <summary>Persists buzz sessions, presses and device mappings, ranks
/// presses, and maps devices to teams (05-API-Design.md §IBuzzerSessionService).
/// Implemented by the buzzer module; without it, the default throws
/// <see cref="Domain.Common.Exceptions.BuzzerUnavailableException"/> (503)
/// from every operation, and reports the buzzer as unavailable in the live state.</summary>
public interface IBuzzerSessionService
{
    Task SetMappingsAsync(Guid matchId, IReadOnlyList<DeviceMappingInput> mappings, CancellationToken cancellationToken);

    Task<BuzzSessionDto> ArmAsync(Guid matchId, Guid segmentId, Guid matchQuestionId, CancellationToken cancellationToken);

    Task<BuzzSessionDto> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Records a device's press, pushed by the agent. The first press
    /// per device wins; a repeat is ignored. Returns the session as it now stands.</summary>
    Task<BuzzSessionDto> SubmitPressAsync(Guid sessionId, int deviceId, long elapsedMs, string? rawFrameHex, CancellationToken cancellationToken);

    Task<BuzzSessionDto> CollectAsync(Guid sessionId, CancellationToken cancellationToken);

    Task ResetAsync(Guid sessionId, string? reason, CancellationToken cancellationToken);

    Task<LiveBuzzerStatusDto> LiveStatusAsync(Guid matchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArmedBuzzSessionDto>> ArmedSessionsAsync(CancellationToken cancellationToken);
}
