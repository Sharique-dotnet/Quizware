using Quizware.Application.Qualification.Dtos;

namespace Quizware.Api.Contracts.V1.Qualification;

/// <summary>Maps the Application's qualification DTOs to the frozen API contracts.</summary>
public static class QualificationMapper
{
    public static QualificationPreviewResponse ToResponse(QualificationPreviewDto dto) =>
        new(
            ToRef(dto.FromStage),
            dto.ToStage is null ? null : ToRef(dto.ToStage),
            new QualificationRulePreviewDto(dto.Rule.WinnersPerMatch, dto.Rule.BestRemainingAcrossStage, dto.Rule.ManualWildcardSlots, dto.Rule.TotalSlots),
            dto.AllMatchesComplete,
            dto.Qualifiers.Select(q => new QualifierDto(q.TeamId, q.TeamName, q.SourceMatch, q.StageScore, q.StageRank, q.Reason)).ToList(),
            dto.Eliminated.Select(e => new EliminatedTeamDto(e.TeamId, e.TeamName, e.StageScore, e.StageRank)).ToList(),
            dto.UnresolvedTies
                .Select(t => new UnresolvedTieDto(
                    t.Rank,
                    t.Teams.Select(team => new UnresolvedTieTeamDto(team.TeamId, team.TeamName, team.StageScore)).ToList(),
                    t.CriteriaApplied,
                    t.StillTied,
                    t.ResolutionOptions))
                .ToList(),
            dto.CanCommit,
            dto.BlockedReason);

    private static Contracts.V1.Qualification.StageRefDto ToRef(Application.Qualification.Dtos.StageRefDto stage) =>
        new(stage.Id, stage.Name, stage.OrderIndex);
}
