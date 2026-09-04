using QuizApp.Domain.Common;
using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Scoring;

/// <summary>The fast read model, updated incrementally inside the same
/// transaction as the score event — never recomputed from raw answers on a request.</summary>
public sealed class TeamMatchScore : BaseEntity, ITenantScoped
{
    private TeamMatchScore()
    {
    }

    public static TeamMatchScore CreateForParticipant(Guid programId, Guid matchId, Guid teamId, Guid matchParticipantId)
    {
        return new TeamMatchScore
        {
            ProgramId = programId,
            MatchId = matchId,
            TeamId = teamId,
            MatchParticipantId = matchParticipantId,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid MatchParticipantId { get; private set; }
    public int TotalPoints { get; private set; }
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    public int NoAnswerCount { get; private set; }
    public int PassedCount { get; private set; }
    public string? PointsByFormatJson { get; private set; }
    public int? AverageBuzzTimeMs { get; private set; }
    public int? Rank { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }

    public void ApplyAnswer(AnswerOutcome outcome, int points)
    {
        TotalPoints += points;

        switch (outcome)
        {
            case AnswerOutcome.Correct or AnswerOutcome.PassedCorrect:
                CorrectCount++;
                break;
            case AnswerOutcome.Incorrect or AnswerOutcome.PassedIncorrect:
                IncorrectCount++;
                break;
            case AnswerOutcome.NoAnswer:
                NoAnswerCount++;
                break;
            case AnswerOutcome.Passed:
                PassedCount++;
                break;
        }

        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void ApplyAdjustment(int points)
    {
        TotalPoints += points;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void SetRank(int rank)
    {
        Rank = rank;
    }
}
