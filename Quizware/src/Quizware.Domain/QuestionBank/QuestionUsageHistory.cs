using Quizware.Domain.Common;

namespace Quizware.Domain.QuestionBank;

/// <summary>Drives repeat-policy exclusion (NeverInMatch/Stage/Program/ForTeam).</summary>
public sealed class QuestionUsageHistory : BaseEntity, ITenantScoped
{
    private QuestionUsageHistory()
    {
    }

    public static QuestionUsageHistory Record(
        Guid programId, Guid questionId, Guid matchId, Guid? stageId = null, Guid? teamId = null)
    {
        return new QuestionUsageHistory
        {
            ProgramId = programId,
            QuestionId = questionId,
            MatchId = matchId,
            StageId = stageId,
            TeamId = teamId,
            UsedAtUtc = DateTime.UtcNow,
        };
    }

    public Guid ProgramId { get; private set; }
    public Guid QuestionId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid? StageId { get; private set; }
    public Guid? TeamId { get; private set; }
    public DateTime UsedAtUtc { get; private set; }
}
