using Quizware.Domain.Tournament;

namespace Quizware.Application.Tournament.Dtos;

internal static class StageMappings
{
    public static SegmentTemplateDto ToDto(this StageSegmentTemplate segment) =>
        new(segment.Id, segment.FormatCode.ToString(), segment.OrderIndex, segment.QuestionCount, segment.IsOrderLocked);

    public static StageDto ToDto(this Stage stage, IReadOnlyList<StageSegmentTemplate> segments) =>
        new(
            stage.Id,
            stage.Name,
            stage.OrderIndex,
            stage.State.ToString(),
            stage.SegmentOrderMode.ToString(),
            segments.OrderBy(s => s.OrderIndex).Select(s => s.ToDto()).ToList());

    public static StageSummaryDto ToSummaryDto(this Stage stage) =>
        new(stage.Id, stage.Name, stage.OrderIndex, stage.State.ToString());
}
