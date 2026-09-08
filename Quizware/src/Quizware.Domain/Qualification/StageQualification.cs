using Quizware.Domain.Common;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Qualification;

/// <summary>The recorded outcome of qualification from one stage to the next.</summary>
public sealed class StageQualification : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    private StageQualification()
    {
        CreatedBy = string.Empty;
    }

    public static StageQualification Create(
        Guid programId, Guid fromStageId, Guid teamId, QualificationReason reason,
        int stageScore, int stageRank, string createdBy,
        Guid? toStageId = null, Guid? sourceMatchId = null, Guid? tieBreakEventId = null)
    {
        return new StageQualification
        {
            ProgramId = programId,
            FromStageId = fromStageId,
            ToStageId = toStageId,
            TeamId = teamId,
            SourceMatchId = sourceMatchId,
            QualificationReason = reason,
            TieBreakEventId = tieBreakEventId,
            StageScore = stageScore,
            StageRank = stageRank,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid FromStageId { get; private set; }
    public Guid? ToStageId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid? SourceMatchId { get; private set; }
    public QualificationReason QualificationReason { get; private set; }
    public Guid? TieBreakEventId { get; private set; }
    public int StageScore { get; private set; }
    public int StageRank { get; private set; }
    public bool IsCommitted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void Commit()
    {
        IsCommitted = true;
    }
}
