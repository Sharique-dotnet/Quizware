namespace QuizApp.Api.Contracts.V1.Matches;

public sealed record MatchSummaryResponse(Guid Id, string Name, Guid StageId, string State, int MatchNumber);

public sealed record MatchParticipantDto(Guid Id, Guid TeamId, string TeamName, int SeatNumber, int TurnOrder, string Status);

public sealed record MatchDetailResponse(
    Guid Id,
    string Name,
    Guid StageId,
    string State,
    int MatchNumber,
    string MatchKind,
    IReadOnlyList<MatchParticipantDto> Participants);

public sealed record CreateMatchRequest(Guid StageId, string Name, int MatchNumber);

public sealed record UpdateMatchRequest(string Name, int MatchNumber);

public sealed record AddMatchParticipantRequest(Guid TeamId, int SeatNumber);

public sealed record OrderMatchParticipantsRequest(IReadOnlyList<MatchParticipantOrderEntry> Participants);

public sealed record MatchParticipantOrderEntry(Guid ParticipantId, int SeatNumber, int TurnOrder);

public sealed record MatchSegmentSummaryDto(Guid Id, string FormatCode, int OrderIndex, string State, bool IsOrderLocked);

public sealed record MatchSegmentsResponse(IReadOnlyList<MatchSegmentSummaryDto> Segments);

public sealed record ReorderMatchSegmentsRequest(IReadOnlyList<Guid> OrderedSegmentIds, string Reason);

public sealed record AddMatchSegmentRequest(string FormatCode, int QuestionCount);

public sealed record MatchReadyResponse(bool IsReady, IReadOnlyList<string> Blockers);

public sealed record AutoSeedMatchesRequest(Guid StageId, string SeedingMode);

public sealed record AutoSeedMatchesResponse(int MatchesCreated);

public sealed record MatchPreflightResponse(bool QuestionsReady, bool BuzzerHealthy, bool DisplaysOnline, IReadOnlyList<string> Warnings);
