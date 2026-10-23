namespace Quizware.Api.Contracts.V1.Buzzer;

public sealed record BuzzerCapabilityResponse(bool Available, string? Provider, int DeviceCount);

/// <summary>Fields after <see cref="Detail"/> were added in Phase 14.</summary>
public sealed record BuzzerHealthResponse(bool Healthy, string? Detail, string Provider, IReadOnlyList<BuzzerDeviceDto> Devices);

/// <summary><see cref="ButtonTimesMs"/> (button key → milliseconds, 0 = not
/// pressed) and <see cref="RawFrameHex"/> are filled by the test endpoint only;
/// both were added in Phase 14.</summary>
public sealed record BuzzerDeviceDto(
    int DeviceId, string? ComPort, bool IsOnline, IReadOnlyDictionary<string, int>? ButtonTimesMs = null, string? RawFrameHex = null);

public sealed record BuzzerDevicesResponse(IReadOnlyList<BuzzerDeviceDto> Devices);

public sealed record DeviceMappingEntry(int DeviceId, Guid MatchParticipantId);

public sealed record SetDeviceMappingsRequest(IReadOnlyList<DeviceMappingEntry> Mappings);

public sealed record ArmBuzzSessionRequest(Guid SegmentId, Guid MatchQuestionId);

public sealed record ArmBuzzSessionResponse(Guid SessionId, string State);

/// <summary><see cref="MatchParticipantId"/> is null for a device with no
/// mapping (it was a non-null Guid while the endpoint was a stub). Fields
/// after <see cref="Rank"/> were added in Phase 14.</summary>
public sealed record BuzzPressDto(
    int DeviceId,
    Guid? MatchParticipantId,
    long ElapsedMs,
    int Rank,
    Guid PressId,
    Guid? TeamId,
    string? TeamName,
    string ButtonKey,
    string? RawFrameHex,
    DateTime ReceivedAtUtc);

/// <summary>Fields after <see cref="RankedPresses"/> were added in Phase 14.</summary>
public sealed record BuzzSessionResponse(
    Guid SessionId,
    string State,
    IReadOnlyList<BuzzPressDto> RankedPresses,
    Guid MatchId,
    Guid? MatchQuestionId,
    string Provider,
    int WindowMs,
    string? FailureReason,
    DateTime? ArmedAtUtc,
    DateTime? CollectedAtUtc);

/// <summary><see cref="ButtonKey"/> (which button on the device, e.g. "A")
/// was added in Phase 14 and is optional.</summary>
public sealed record SubmitBuzzPressRequest(int DeviceId, long ElapsedMs, string RawFrameHex, string? ButtonKey = null);

public sealed record ResetBuzzSessionRequest(string? Reason);

public sealed record BuzzerTestResponse(IReadOnlyList<BuzzerDeviceDto> Devices);

/// <summary>The buzzer agent's heartbeat (added in Phase 14): who it is, which
/// port it owns, and what each device last read.</summary>
public sealed record AgentHeartbeatRequest(string AgentId, string? PortName, IReadOnlyList<AgentDeviceReportDto> Devices);

public sealed record AgentDeviceReportDto(int DeviceId, bool IsOnline, IReadOnlyDictionary<string, int>? ButtonTimesMs, string? RawFrameHex);

/// <summary>The sessions the agent should be polling, and when to call back.</summary>
public sealed record AgentHeartbeatResponse(IReadOnlyList<AgentArmedSessionDto> ArmedSessions, int PollIntervalMs);

public sealed record AgentArmedSessionDto(Guid SessionId, IReadOnlyList<int> DeviceIds, int WindowMs, DateTime ArmedAtUtc);
