namespace Quizware.Application.Buzzer;

public sealed record BuzzerDeviceStatus(int DeviceId, string? Port, bool IsOnline);

public sealed record BuzzerHealth(bool Healthy, string? Detail, IReadOnlyList<BuzzerDeviceStatus> Devices);

public sealed record BuzzArmRequest(Guid SessionId, Guid MatchId, IReadOnlyList<int> DeviceIds, int WindowMs);

/// <summary>One device's first press in a session. <see cref="ElapsedMs"/> is
/// measured by the device from the moment it was armed.</summary>
public sealed record BuzzPressResult(int DeviceId, string ButtonKey, int ElapsedMs, string? RawFrameHex);

/// <summary>Every button's timer on one device — the rehearsal ("test") view.
/// 0 means the button was not pressed.</summary>
public sealed record BuzzDeviceReading(int DeviceId, bool IsOnline, IReadOnlyDictionary<string, int> ButtonTimesMs, string? RawFrameHex);

/// <summary>The optional buzzer port (ADR-005). The default adapter is
/// <see cref="NullBuzzerAdapter"/>; the buzzer module replaces it with a
/// serial or HTTP-agent adapter by configuration. A buzzer only says who
/// pressed first — it never awards points. Adapters throw
/// <see cref="Domain.Common.Exceptions.BuzzerUnavailableException"/> when they
/// cannot reach the hardware.</summary>
public interface IBuzzerProvider
{
    /// <summary>"None", "Serial" or "HttpAgent".</summary>
    string Name { get; }

    bool IsAvailable { get; }

    /// <summary>The configured device count — a default, not a limit.</summary>
    int DeviceCount { get; }

    Task<BuzzerHealth> CheckHealthAsync(CancellationToken cancellationToken);

    Task ArmAsync(BuzzArmRequest request, CancellationToken cancellationToken);

    /// <summary>Presses the provider can read now. A push-based provider
    /// (the HTTP agent) returns none here — its presses arrive through the
    /// ingestion endpoint instead.</summary>
    Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken);

    Task ResetAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<BuzzDeviceReading>> TestAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken);
}
