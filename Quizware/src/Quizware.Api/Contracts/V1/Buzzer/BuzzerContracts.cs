namespace Quizware.Api.Contracts.V1.Buzzer;

public sealed record BuzzerCapabilityResponse(bool Available, string? Provider, int DeviceCount);

public sealed record BuzzerHealthResponse(bool Healthy, string? Detail);

public sealed record BuzzerDeviceDto(int DeviceId, string? ComPort, bool IsOnline);

public sealed record BuzzerDevicesResponse(IReadOnlyList<BuzzerDeviceDto> Devices);

public sealed record DeviceMappingEntry(int DeviceId, Guid MatchParticipantId);

public sealed record SetDeviceMappingsRequest(IReadOnlyList<DeviceMappingEntry> Mappings);

public sealed record ArmBuzzSessionRequest(Guid SegmentId, Guid MatchQuestionId);

public sealed record ArmBuzzSessionResponse(Guid SessionId, string State);

public sealed record BuzzPressDto(int DeviceId, Guid MatchParticipantId, long ElapsedMs, int Rank);

public sealed record BuzzSessionResponse(Guid SessionId, string State, IReadOnlyList<BuzzPressDto> RankedPresses);

public sealed record SubmitBuzzPressRequest(int DeviceId, long ElapsedMs, string RawFrameHex);

public sealed record ResetBuzzSessionRequest(string? Reason);

public sealed record BuzzerTestResponse(IReadOnlyList<BuzzerDeviceDto> Devices);
