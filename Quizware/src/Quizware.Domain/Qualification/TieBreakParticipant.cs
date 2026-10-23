using Quizware.Domain.Common;

namespace Quizware.Domain.Qualification;

/// <summary>Records exactly who was tied, what they were tied on, and how
/// they finished — the evidence behind the qualification decision.</summary>
public sealed class TieBreakParticipant : BaseEntity, IAuditable, ISoftDeletable
{
    private TieBreakParticipant()
    {
        CreatedBy = string.Empty;
    }

    public static TieBreakParticipant Create(Guid tieBreakEventId, Guid teamId, int enteringScore, string createdBy)
    {
        return new TieBreakParticipant
        {
            TieBreakEventId = tieBreakEventId,
            TeamId = teamId,
            EnteringScore = enteringScore,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid TieBreakEventId { get; private set; }
    public Guid TeamId { get; private set; }
    public int EnteringScore { get; private set; }
    public int TieBreakPoints { get; private set; }
    public int? ResultRank { get; private set; }
    public bool Qualified { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Still contested: no place has been decided for this team yet.</summary>
    public bool IsContested => ResultRank is null;

    public void RecordResult(int tieBreakPoints, int resultRank, bool qualified)
    {
        TieBreakPoints = tieBreakPoints;
        ResultRank = resultRank;
        Qualified = qualified;
    }

    /// <summary>A tie-break round ended level for this team: its points are
    /// kept, but its place is still contested.</summary>
    public void RecordPoints(int tieBreakPoints)
    {
        TieBreakPoints = tieBreakPoints;
    }
}
