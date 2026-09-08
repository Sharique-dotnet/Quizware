namespace Quizware.Api.Contracts.V1.Stages;

public sealed record StageSegmentTemplateDto(Guid Id, string FormatCode, int OrderIndex, int QuestionCount, bool IsOrderLocked);

public sealed record StageSummaryResponse(Guid Id, string Name, int OrderIndex, string State);

public sealed record StageDetailResponse(
    Guid Id,
    string Name,
    int OrderIndex,
    string State,
    string SegmentOrderMode,
    IReadOnlyList<StageSegmentTemplateDto> Segments);

public sealed record CreateStageRequest(string Name, int OrderIndex, string StageType = "League");

public sealed record UpdateStageRequest(string Name);

public sealed record ReorderStagesRequest(IReadOnlyList<Guid> OrderedStageIds);

public sealed record CreateSegmentTemplateRequest(string FormatCode, int QuestionCount, bool IsOrderLocked = false);

public sealed record UpdateSegmentTemplateRequest(int QuestionCount, bool IsOrderLocked);

public sealed record ReorderSegmentTemplatesRequest(IReadOnlyList<Guid> OrderedSegmentTemplateIds);

public sealed record ReorderSegmentTemplatesResponse(
    Guid StageId,
    string SegmentOrderMode,
    IReadOnlyList<StageSegmentTemplateDto> Segments,
    AffectedMatchesDto AffectedMatches);

public sealed record AffectedMatchesDto(string NotYetCreated, int AlreadyCreated, int InProgress);

public sealed record SetSegmentOrderModeRequest(string SegmentOrderMode);

public sealed record StageValidationResponse(bool IsRunnable, IReadOnlyList<string> Blockers);
