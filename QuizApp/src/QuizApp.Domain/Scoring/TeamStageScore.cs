using QuizApp.Domain.Common;

namespace QuizApp.Domain.Scoring;

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

    public void RecordMatchResult(int points, bool won)
    {
        TotalPoints += points;
        MatchesPlayed++;

        if (won)
        {
            Wins++;
        }
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
