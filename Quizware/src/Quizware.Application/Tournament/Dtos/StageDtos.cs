namespace Quizware.Application.Tournament.Dtos;

public sealed record SegmentTemplateDto(Guid Id, string FormatCode, int OrderIndex, int QuestionCount, bool IsOrderLocked);

public sealed record StageDto(
    Guid Id,
    string Name,
    int OrderIndex,
    string State,
    string SegmentOrderMode,
    IReadOnlyList<SegmentTemplateDto> Segments);

public sealed record StageSummaryDto(Guid Id, string Name, int OrderIndex, string State);

public sealed record StageValidationDto(bool IsRunnable, IReadOnlyList<string> Blockers);

public sealed record ReorderSegmentTemplatesResultDto(
    Guid StageId,
    string SegmentOrderMode,
    IReadOnlyList<SegmentTemplateDto> Segments,
    string AffectedMatchesNotYetCreated,
    int AffectedMatchesAlreadyCreated,
    int AffectedMatchesInProgress);
