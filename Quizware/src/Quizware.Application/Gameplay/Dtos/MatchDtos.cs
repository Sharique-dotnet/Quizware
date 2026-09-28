namespace Quizware.Application.Gameplay.Dtos;

public sealed record MatchSummaryDto(Guid Id, string Name, Guid StageId, string State, int MatchNumber);

public sealed record ParticipantDto(Guid Id, Guid TeamId, string TeamName, int SeatNumber, int TurnOrder, string Status);

public sealed record MatchDto(
    Guid Id,
    string Name,
    Guid StageId,
    string State,
    int MatchNumber,
    string MatchKind,
    IReadOnlyList<ParticipantDto> Participants);

public sealed record MatchSegmentDto(
    Guid Id, string FormatCode, int OrderIndex, int PlannedQuestionCount, string State, bool IsOrderLocked);

public sealed record MatchReadyDto(bool IsReady, IReadOnlyList<string> Blockers);
