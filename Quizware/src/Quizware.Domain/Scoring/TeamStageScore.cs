using Quizware.Domain.Common;

namespace Quizware.Domain.Scoring;

/// <summary>Powers the leaderboard and the "best remaining" qualification
/// rule without scanning every answer.</summary>
public sealed class TeamStageScore : BaseEntity, ITenantScoped
{
    private TeamStageScore()
    {
    }

    public static TeamStageScore CreateForTeam(Guid programId, Guid stageId, Guid teamId)
    {
        return new TeamStageScore
        {
            ProgramId = programId,
            StageId = stageId,
            TeamId = teamId,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid StageId { get; private set; }
    public Guid TeamId { get; private set; }
    public int TotalPoints { get; private set; }
    public int MatchesPlayed { get; private set; }
    public int Wins { get; private set; }
    public int? Rank { get; private set; }
    public bool QualifiedFlag { get; private set; }

    /// <summary>Every score event in a match that counts toward the stage
    /// lands here too, in the same transaction — the stage total is never
    /// recomputed on a read.</summary>
    public void ApplyPoints(int points)
    {
        TotalPoints += points;
    }

    public void RecordMatchCompleted(bool won)
    {
        MatchesPlayed++;

        if (won)
        {
            Wins++;
        }
    }

    /// <summary>Overwrites the totals with values rebuilt from the ledger.</summary>
    public void Rebuild(int totalPoints, int matchesPlayed, int wins)
    {
        TotalPoints = totalPoints;
        MatchesPlayed = matchesPlayed;
        Wins = wins;
    }

    public void SetRank(int rank)
    {
        Rank = rank;
    }

    public void MarkQualified()
    {
        QualifiedFlag = true;
    }
}
