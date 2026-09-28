using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Qualification;

public sealed class TieBreakCriteriaService : ITieBreakCriteriaService
{
    public const string TotalScore = "TotalScore";
    public const string FewerIncorrect = "FewerIncorrect";
    public const string MoreCorrectAtHighDifficulty = "MoreCorrectAtHighDifficulty";
    public const string FasterAverageBuzzTime = "FasterAverageBuzzTime";
    public const string HeadToHead = "HeadToHead";

    /// <summary>04-Database-Schema.md §TieBreakRule's documented order —
    /// cheap checks first — used when neither the stage nor the program has
    /// a stage-qualification rule.</summary>
    public static readonly IReadOnlyList<string> DefaultCriteria =
        [TotalScore, FewerIncorrect, MoreCorrectAtHighDifficulty, FasterAverageBuzzTime, HeadToHead];

    /// <summary>The Phase 7 default rules spell one criterion differently.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HigherDifficultyCorrect"] = MoreCorrectAtHighDifficulty,
    };

    private readonly IAppDbContext _db;

    public TieBreakCriteriaService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> CriteriaForStageAsync(Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await _db.Stages.SingleOrDefaultAsync(s => s.Id == stageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage '{stageId}' was not found.");
        var rules = await _db.TieBreakRules
            .Where(r => r.ProgramId == stage.ProgramId && r.Scope == TieBreakScope.StageQualification
                && (r.StageId == stageId || r.StageId == null))
            .ToListAsync(cancellationToken);
        var rule = rules.OrderByDescending(r => r.StageId is not null).FirstOrDefault();

        return rule is null
            ? DefaultCriteria
            : rule.Criteria.Select(c => Aliases.GetValueOrDefault(c, c)).ToList();
    }

    public async Task<TieBreakOrdering> OrderTiedTeamsAsync(
        Guid stageId, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        var criteria = await CriteriaForStageAsync(stageId, cancellationToken);
        if (teamIds.Count < 2)
        {
            return new TieBreakOrdering(teamIds.Select(id => (IReadOnlyList<Guid>)[id]).ToList(), null, []);
        }

        var trace = new List<CriterionTrace>();
        foreach (var criterion in criteria)
        {
            trace.Add(await TraceAsync(stageId, teamIds, criterion, cancellationToken));
        }

        var candidates = teamIds
            .Select(id => new TieBreakCandidate(id, trace.ToDictionary(t => t.Criterion, t => t.Values.GetValueOrDefault(id))))
            .ToList();

        var groups = new List<IReadOnlyList<Guid>>();
        string? decidingCriterion = null;
        var remaining = candidates;
        while (remaining.Count > 0)
        {
            var result = TieBreakCriteriaEvaluator.Evaluate(remaining, criteria);
            if (groups.Count == 0 && result.IsResolved && remaining.Count > 1)
            {
                decidingCriterion = result.DecidingCriterion;
            }

            groups.Add(result.TeamIds);
            remaining = remaining.Where(c => !result.TeamIds.Contains(c.TeamId)).ToList();
        }

        return new TieBreakOrdering(groups, decidingCriterion, trace);
    }

    private async Task<CriterionTrace> TraceAsync(
        Guid stageId, IReadOnlyList<Guid> teamIds, string criterion, CancellationToken cancellationToken)
    {
        var matchIds = await _db.Matches
            .Where(m => m.StageId == stageId && m.MatchKind == MatchKind.Regular
                && m.State != MatchState.Abandoned && m.State != MatchState.Draft && m.State != MatchState.Ready)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        (Dictionary<Guid, decimal> Values, string? Note) result = criterion switch
        {
            TotalScore => (await _db.TeamStageScores
                .Where(s => s.StageId == stageId && teamIds.Contains(s.TeamId))
                .ToDictionaryAsync(s => s.TeamId, s => (decimal)s.TotalPoints, cancellationToken), null),
            FewerIncorrect => (await FewerIncorrectAsync(matchIds, teamIds, cancellationToken), null),
            MoreCorrectAtHighDifficulty => (await HighDifficultyCorrectAsync(matchIds, teamIds, cancellationToken), null),
            FasterAverageBuzzTime => await BuzzTimeAsync(matchIds, teamIds, cancellationToken),
            HeadToHead => await HeadToHeadAsync(matchIds, teamIds, cancellationToken),
            _ => (new Dictionary<Guid, decimal>(), $"Unknown criterion '{criterion}' — skipped."),
        };

        var values = teamIds.ToDictionary(id => id, id => result.Values.GetValueOrDefault(id));
        return new CriterionTrace(criterion, values.Values.Distinct().Count() > 1, values, result.Note);
    }

    private async Task<Dictionary<Guid, decimal>> FewerIncorrectAsync(
        List<Guid> matchIds, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        var incorrect = await _db.TeamMatchScores
            .Where(s => matchIds.Contains(s.MatchId) && teamIds.Contains(s.TeamId))
            .GroupBy(s => s.TeamId)
            .Select(g => new { TeamId = g.Key, Incorrect = g.Sum(s => s.IncorrectCount) })
            .ToDictionaryAsync(x => x.TeamId, x => x.Incorrect, cancellationToken);
        return teamIds.ToDictionary(id => id, id => -(decimal)incorrect.GetValueOrDefault(id));
    }

    private async Task<Dictionary<Guid, decimal>> HighDifficultyCorrectAsync(
        List<Guid> matchIds, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        var counts = await (
            from answer in _db.AnswerRecords
            where matchIds.Contains(answer.MatchId) && teamIds.Contains(answer.TeamId) && !answer.IsReversed
                && (answer.Outcome == AnswerOutcome.Correct || answer.Outcome == AnswerOutcome.PassedCorrect)
            join served in _db.MatchQuestions on answer.MatchQuestionId equals served.Id
            join question in _db.Questions.IgnoreQueryFilters() on served.QuestionId equals question.Id
            where question.DifficultyLevel >= DifficultyLevel.Hard
            group answer by answer.TeamId into g
            select new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count, cancellationToken);
        return teamIds.ToDictionary(id => id, id => (decimal)counts.GetValueOrDefault(id));
    }

    /// <summary>Faster is better, so the value is the negated average. A team
    /// with no buzzer answers ranks behind every team that has some; if none
    /// of them do, the criterion cannot separate them.</summary>
    private async Task<(Dictionary<Guid, decimal>, string?)> BuzzTimeAsync(
        List<Guid> matchIds, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        var averages = await _db.AnswerRecords
            .Where(a => matchIds.Contains(a.MatchId) && teamIds.Contains(a.TeamId) && !a.IsReversed
                && a.AnswerSource == AnswerSource.Buzzer && a.ResponseTimeMs != null)
            .GroupBy(a => a.TeamId)
            .Select(g => new { TeamId = g.Key, Average = g.Average(a => (double)a.ResponseTimeMs!.Value) })
            .ToDictionaryAsync(x => x.TeamId, x => x.Average, cancellationToken);

        if (averages.Count == 0)
        {
            return (teamIds.ToDictionary(id => id, _ => 0m), "No buzzer data in this stage.");
        }

        return (teamIds.ToDictionary(
            id => id,
            id => averages.TryGetValue(id, out var average) ? -(decimal)average : decimal.MinValue / 2), null);
    }

    /// <summary>Head-to-head wins among the tied teams only: in each completed
    /// match two of them both played, the one with more points takes it.</summary>
    private async Task<(Dictionary<Guid, decimal>, string?)> HeadToHeadAsync(
        List<Guid> matchIds, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        var completed = await _db.Matches
            .Where(m => matchIds.Contains(m.Id) && m.State == MatchState.Completed)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var scores = await _db.TeamMatchScores
            .Where(s => completed.Contains(s.MatchId) && teamIds.Contains(s.TeamId))
            .ToListAsync(cancellationToken);

        var wins = teamIds.ToDictionary(id => id, _ => 0m);
        var met = false;
        foreach (var match in scores.GroupBy(s => s.MatchId).Where(g => g.Count() > 1))
        {
            met = true;
            foreach (var score in match)
            {
                wins[score.TeamId] += match.Count(other => other.TeamId != score.TeamId && other.TotalPoints < score.TotalPoints);
            }
        }

        return (wins, met ? null : "Teams never met.");
    }
}
