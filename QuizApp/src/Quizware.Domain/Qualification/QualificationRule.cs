using Quizware.Domain.Common;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Qualification;

/// <summary>Who advances — the rule that currently exists only in people's heads.</summary>
public sealed class QualificationRule : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private QualificationRule()
    {
        CreatedBy = string.Empty;
    }

    public static QualificationRule Create(
        Guid programId, Guid fromStageId, string createdBy, Guid? toStageId = null,
        int winnersPerMatch = 1, int bestRemainingAcrossStage = 0, int manualWildcardSlots = 0,
        int? minScoreThreshold = null, Guid? tieBreakRuleId = null, SeedingMode seedingMode = SeedingMode.Manual,
        string? description = null)
    {
        return new QualificationRule
        {
            ProgramId = programId,
            FromStageId = fromStageId,
            ToStageId = toStageId,
            WinnersPerMatch = winnersPerMatch,
            BestRemainingAcrossStage = bestRemainingAcrossStage,
            ManualWildcardSlots = manualWildcardSlots,
            MinScoreThreshold = minScoreThreshold,
            TieBreakRuleId = tieBreakRuleId,
            SeedingMode = seedingMode,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid FromStageId { get; private set; }
    public Guid? ToStageId { get; private set; }
    public int WinnersPerMatch { get; private set; }
    public int BestRemainingAcrossStage { get; private set; }
    public int ManualWildcardSlots { get; private set; }
    public int? MinScoreThreshold { get; private set; }
    public Guid? TieBreakRuleId { get; private set; }
    public SeedingMode SeedingMode { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void Update(int winnersPerMatch, int bestRemainingAcrossStage, int manualWildcardSlots, string updatedBy)
    {
        WinnersPerMatch = winnersPerMatch;
        BestRemainingAcrossStage = bestRemainingAcrossStage;
        ManualWildcardSlots = manualWildcardSlots;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
