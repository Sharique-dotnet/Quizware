using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Scoring;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Scoring;

public sealed class ScoringEngine : IScoringEngine
{
    private readonly IAppDbContext _db;
    private readonly ScoringResolver _rules;
    private readonly Dictionary<Guid, bool> _countsTowardStage = new();

    public ScoringEngine(IAppDbContext db, ScoringResolver rules)
    {
        _db = db;
        _rules = rules;
    }

    public async Task<ScoredAnswer> ScoreAnswerAsync(
        Match match, MatchSegment segment, MatchParticipant participant, AnswerRecord answer, Guid userId,
        CancellationToken cancellationToken)
    {
        var rule = await _rules.ResolveAsync(match, segment, answer.Outcome, answer.PassNumber, cancellationToken);
        _db.ScoreEvents.Add(ScoreEvent.ForAnswer(
            match.ProgramId, match.Id, participant.TeamId, participant.Id, answer.Id, rule.Id, rule.Points, userId, segment.Id));
        await ApplyAsync(match, participant.Id, participant.TeamId, s => s.ApplyAnswer(answer.Outcome, rule.Points), rule.Points, cancellationToken);
        return new ScoredAnswer(rule, rule.Points);
    }

    public async Task<int> ScorePassAsync(
        Match match, MatchSegment segment, MatchParticipant participant, AnswerRecord pass, Guid userId,
        CancellationToken cancellationToken)
    {
        var rule = await _rules.TryResolveAsync(match, segment, AnswerOutcome.Passed, pass.PassNumber, cancellationToken);
        var points = rule?.Points ?? 0;
        if (rule is not null)
        {
            _db.ScoreEvents.Add(ScoreEvent.ForAnswer(
                match.ProgramId, match.Id, participant.TeamId, participant.Id, pass.Id, rule.Id, points, userId, segment.Id));
        }

        await ApplyAsync(match, participant.Id, participant.TeamId, s => s.ApplyAnswer(AnswerOutcome.Passed, points), points, cancellationToken);
        return points;
    }

    public async Task<ReversedScore> ReverseAnswerAsync(
        Match match, AnswerRecord original, Guid userId, string reason, CancellationToken cancellationToken)
    {
        var scoreEvent = await _db.ScoreEvents
            .SingleOrDefaultAsync(e => e.AnswerRecordId == original.Id && e.EventType == ScoreEventType.Answer, cancellationToken)
            ?? throw new InvalidStateTransitionException("This answer has no score event to reverse.");
        if (scoreEvent.IsReversed)
        {
            throw new InvalidStateTransitionException("This answer's score has already been reversed.");
        }

        _db.ScoreEvents.Add(scoreEvent.Reverse(userId, reason));
        await ApplyAsync(
            match, original.MatchParticipantId, original.TeamId,
            s => s.RevertAnswer(original.Outcome, scoreEvent.Points), -scoreEvent.Points, cancellationToken);
        return new ReversedScore(scoreEvent.ScoringRuleId!.Value, scoreEvent.Points);
    }

    public async Task<ScoreEvent> AdjustAsync(
        Match match, MatchParticipant participant, int points, string reason, Guid approvedByUserId,
        CancellationToken cancellationToken)
    {
        var adjustment = ScoreEvent.ManualAdjustment(
            match.ProgramId, match.Id, participant.TeamId, participant.Id, points, reason, approvedByUserId);
        _db.ScoreEvents.Add(adjustment);
        await ApplyAsync(match, participant.Id, participant.TeamId, s => s.ApplyAdjustment(points), points, cancellationToken);
        return adjustment;
    }

    public async Task OpenScoresAsync(Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken)
    {
        foreach (var participant in participants)
        {
            _db.TeamMatchScores.Add(TeamMatchScore.CreateForParticipant(match.ProgramId, match.Id, participant.TeamId, participant.Id));
        }

        if (await CountsTowardStageAsync(match, cancellationToken))
        {
            foreach (var participant in participants)
            {
                await StageScoreAsync(match, participant.TeamId, cancellationToken);
            }
        }
    }

    public async Task RecordMatchCompletedAsync(
        Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken)
    {
        if (!await CountsTowardStageAsync(match, cancellationToken))
        {
            return;
        }

        foreach (var participant in participants)
        {
            var stageScore = await StageScoreAsync(match, participant.TeamId, cancellationToken);
            stageScore.RecordMatchCompleted(won: match.WinnerTeamId == participant.TeamId);
        }

        await RerankStageAsync(match.StageId, cancellationToken);
    }

    public async Task WithdrawAbandonedMatchAsync(Match match, CancellationToken cancellationToken)
    {
        if (!await CountsTowardStageAsync(match, cancellationToken))
        {
            return;
        }

        foreach (var matchScore in await _db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken))
        {
            (await StageScoreAsync(match, matchScore.TeamId, cancellationToken)).ApplyPoints(-matchScore.TotalPoints);
        }
    }

    public async Task<IReadOnlyList<TeamMatchScore>> RecalculateMatchAsync(Match match, CancellationToken cancellationToken)
    {
        var scores = await _db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken);
        var pointsByParticipant = await _db.ScoreEvents
            .Where(e => e.MatchId == match.Id)
            .GroupBy(e => e.MatchParticipantId)
            .Select(g => new { ParticipantId = g.Key, Points = g.Sum(e => e.Points) })
            .ToDictionaryAsync(x => x.ParticipantId, x => x.Points, cancellationToken);
        var answers = await _db.AnswerRecords
            .Where(a => a.MatchId == match.Id && !a.IsReversed && a.Outcome != AnswerOutcome.Voided)
            .Select(a => new { a.MatchParticipantId, a.Outcome })
            .ToListAsync(cancellationToken);

        foreach (var score in scores)
        {
            var mine = answers.Where(a => a.MatchParticipantId == score.MatchParticipantId).Select(a => a.Outcome).ToList();
            score.Rebuild(
                pointsByParticipant.GetValueOrDefault(score.MatchParticipantId),
                mine.Count(o => o is AnswerOutcome.Correct or AnswerOutcome.PassedCorrect),
                mine.Count(o => o is AnswerOutcome.Incorrect or AnswerOutcome.PassedIncorrect),
                mine.Count(o => o == AnswerOutcome.NoAnswer),
                mine.Count(o => o == AnswerOutcome.Passed));
        }

        return scores;
    }

    public async Task RecalculateStageAsync(Guid stageId, CancellationToken cancellationToken)
    {
        var matches = await _db.Matches
            .Where(m => m.StageId == stageId && m.State != MatchState.Abandoned
                && m.State != MatchState.Draft && m.State != MatchState.Ready)
            .ToListAsync(cancellationToken);
        var counting = new List<Match>();
        foreach (var match in matches)
        {
            if (await CountsTowardStageAsync(match, cancellationToken))
            {
                counting.Add(match);
            }
        }

        var countingIds = counting.Select(m => m.Id).ToList();
        var matchScores = await _db.TeamMatchScores.Where(s => countingIds.Contains(s.MatchId)).ToListAsync(cancellationToken);
        var stageScores = await _db.TeamStageScores.Where(s => s.StageId == stageId).ToListAsync(cancellationToken);

        foreach (var teamId in matchScores.Select(s => s.TeamId).Union(stageScores.Select(s => s.TeamId)).Distinct())
        {
            var stageScore = stageScores.SingleOrDefault(s => s.TeamId == teamId);
            if (stageScore is null)
            {
                var anyMatch = counting.First(m => matchScores.Any(s => s.MatchId == m.Id && s.TeamId == teamId));
                stageScore = TeamStageScore.CreateForTeam(anyMatch.ProgramId, stageId, teamId);
                _db.TeamStageScores.Add(stageScore);
                stageScores.Add(stageScore);
            }

            var teamMatchScores = matchScores.Where(s => s.TeamId == teamId).ToList();
            var completed = counting.Where(m => m.State == MatchState.Completed && teamMatchScores.Any(s => s.MatchId == m.Id)).ToList();
            stageScore.Rebuild(
                teamMatchScores.Sum(s => s.TotalPoints),
                completed.Count,
                completed.Count(m => m.WinnerTeamId == teamId));
        }

        RankByPoints(stageScores);
    }

    private async Task ApplyAsync(
        Match match, Guid participantId, Guid teamId, Action<TeamMatchScore> applyToMatch, int points,
        CancellationToken cancellationToken)
    {
        var matchScore = _db.TeamMatchScores.Local.SingleOrDefault(s => s.MatchParticipantId == participantId)
            ?? await _db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == participantId, cancellationToken);
        applyToMatch(matchScore);

        if (points != 0 && await CountsTowardStageAsync(match, cancellationToken))
        {
            (await StageScoreAsync(match, teamId, cancellationToken)).ApplyPoints(points);
        }
    }

    /// <summary>Regular matches always count. A tie-break match counts only
    /// when its rule says so (ScoreCountsTowardStage) — by default tie-break
    /// points never change a team's stage total.</summary>
    private async Task<bool> CountsTowardStageAsync(Match match, CancellationToken cancellationToken)
    {
        if (_countsTowardStage.TryGetValue(match.Id, out var counts))
        {
            return counts;
        }

        counts = match.MatchKind == MatchKind.Regular
            || await (from tieBreak in _db.TieBreakEvents
                      join rule in _db.TieBreakRules.IgnoreQueryFilters() on tieBreak.TieBreakRuleId equals rule.Id
                      where tieBreak.Id == match.TieBreakEventId
                      select rule.ScoreCountsTowardStage)
                .SingleOrDefaultAsync(cancellationToken);
        _countsTowardStage[match.Id] = counts;
        return counts;
    }

    private async Task<TeamStageScore> StageScoreAsync(Match match, Guid teamId, CancellationToken cancellationToken)
    {
        var stageScore = _db.TeamStageScores.Local.SingleOrDefault(s => s.StageId == match.StageId && s.TeamId == teamId)
            ?? await _db.TeamStageScores.SingleOrDefaultAsync(s => s.StageId == match.StageId && s.TeamId == teamId, cancellationToken);
        if (stageScore is null)
        {
            stageScore = TeamStageScore.CreateForTeam(match.ProgramId, match.StageId, teamId);
            _db.TeamStageScores.Add(stageScore);
        }

        return stageScore;
    }

    private async Task RerankStageAsync(Guid stageId, CancellationToken cancellationToken)
    {
        var stored = await _db.TeamStageScores.Where(s => s.StageId == stageId).ToListAsync(cancellationToken);
        var all = stored.Union(_db.TeamStageScores.Local.Where(s => s.StageId == stageId)).Distinct().ToList();
        RankByPoints(all);
    }

    /// <summary>Standard competition ranking on points (1, 1, 3). Finer
    /// ordering of level teams is the standings' tie-break criteria's job.</summary>
    private static void RankByPoints(IReadOnlyList<TeamStageScore> scores)
    {
        var ordered = scores.OrderByDescending(s => s.TotalPoints).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SetRank(i > 0 && ordered[i].TotalPoints == ordered[i - 1].TotalPoints ? ordered[i - 1].Rank!.Value : i + 1);
        }
    }
}
