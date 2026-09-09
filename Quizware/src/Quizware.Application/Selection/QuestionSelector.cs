using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Rules.Services;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Selection;

public sealed class QuestionSelector : IQuestionSelector
{
    private readonly IAppDbContext _db;
    private readonly IRuleService _ruleService;

    public QuestionSelector(IAppDbContext db, IRuleService ruleService)
    {
        _db = db;
        _ruleService = ruleService;
    }

    public async Task<SelectionResult> PreviewAsync(SelectionRequest request, CancellationToken cancellationToken)
    {
        var draw = await DrawAsync(request, throwOnExhaustion: false, cancellationToken);

        var questions = draw.Selected
            .Select(q => new SelectedQuestion(q.Id, q.DifficultyLevel, q.TopicId, OptionOrderJson: null))
            .ToList();

        return new SelectionResult(
            questions, draw.PoolSize, draw.EligibleAfterFilters, draw.EligibleAfterRepeatPolicy,
            draw.DifficultyMixRequested, draw.DifficultyMixAchieved, draw.Selected.Count >= request.QuestionCount, draw.Warnings);
    }

    public async Task<SelectionResult> SelectAndReserveAsync(
        SelectionRequest request, Guid matchSegmentId, string reservedBy, CancellationToken cancellationToken)
    {
        if (request.MatchId is null)
        {
            throw new ArgumentException("MatchId is required to reserve a draw.", nameof(request));
        }

        var draw = await DrawAsync(request, throwOnExhaustion: true, cancellationToken);

        var rng = CreateRandom(request.RandomSeed);
        var ordered = draw.Selected.OrderBy(_ => rng.Next()).ToList();

        var questionIds = ordered.Select(q => q.Id).ToList();
        var optionsByQuestion = await _db.QuestionOptions
            .Where(o => questionIds.Contains(o.QuestionId))
            .OrderBy(o => o.Id)
            .GroupBy(o => o.QuestionId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(o => o.Id).ToList(), cancellationToken);

        var reserved = new List<SelectedQuestion>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var question = ordered[i];
            var matchQuestion = MatchQuestion.Reserve(
                request.ProgramId, request.MatchId.Value, matchSegmentId, question.Id, i, reservedBy);
            _db.MatchQuestions.Add(matchQuestion);

            string? optionOrderJson = null;
            if (optionsByQuestion.TryGetValue(question.Id, out var optionIds) && optionIds.Count > 0)
            {
                var shuffled = optionIds.OrderBy(_ => rng.Next()).ToList();
                optionOrderJson = JsonSerializer.Serialize(shuffled);
            }

            reserved.Add(new SelectedQuestion(question.Id, question.DifficultyLevel, question.TopicId, optionOrderJson));
        }

        return new SelectionResult(
            reserved, draw.PoolSize, draw.EligibleAfterFilters, draw.EligibleAfterRepeatPolicy,
            draw.DifficultyMixRequested, draw.DifficultyMixAchieved, CanSatisfy: true, draw.Warnings);
    }

    public async Task ReleaseReservationsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var reserved = await _db.MatchQuestions
            .Where(mq => mq.MatchId == matchId && mq.State == MatchQuestionState.Reserved)
            .ToListAsync(cancellationToken);

        foreach (var matchQuestion in reserved)
        {
            matchQuestion.Release();
        }
    }

    /// <summary>The shared draw: pool building (P8-01), repeat-policy
    /// exclusion (P8-02), difficulty-mix splitting (P8-03), seeded weighted
    /// draw (P8-04), topic spread (P8-05), and the fallback ladder (P8-08).
    /// A preview and a real reservation both go through this so they can
    /// never disagree about what is achievable.</summary>
    private async Task<DrawOutcome> DrawAsync(SelectionRequest request, bool throwOnExhaustion, CancellationToken cancellationToken)
    {
        var rule = await _ruleService.ResolveSelectionRuleAsync(
            request.ProgramId, request.FormatCode, request.StageId, request.SegmentTemplateId, cancellationToken);

        var minDifficulty = rule?.MinDifficultyLevel ?? DifficultyLevel.VeryEasy;
        var maxDifficulty = rule?.MaxDifficultyLevel ?? DifficultyLevel.VeryHard;
        var repeatPolicy = rule?.RepeatPolicy ?? RepeatPolicy.NeverInProgram;
        var topicSpreadPolicy = rule?.TopicSpreadPolicy ?? TopicSpreadPolicy.None;
        var fallbackPolicy = rule?.FallbackPolicy ?? FallbackPolicy.WidenThenFail;
        var topicFilter = ParseGuidSet(rule?.TopicFilterJson);
        var difficultyMixRequested = ParseDifficultyMix(rule?.DifficultyMixJson, request.QuestionCount);

        // The base pool (format + Approved + not deleted, program-owned or
        // shared-library) never changes across ladder steps — every step
        // below only narrows or widens filters applied on top of it.
        var basePool = await _db.Questions
            .Where(q => (q.ProgramId == request.ProgramId || q.ProgramId == null)
                && q.FormatCode == request.FormatCode
                && q.Status == QuestionStatus.Approved
                && !q.IsDeleted)
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);

        var lockedQuestionIds = await _db.MatchQuestions
            .Where(mq => mq.State != MatchQuestionState.Released)
            .Select(mq => mq.QuestionId)
            .ToListAsync(cancellationToken);
        var lockedSet = new HashSet<Guid>(lockedQuestionIds);
        // A draw must never re-select a question it is currently in the
        // middle of reserving elsewhere — except its own match, which owns
        // the lock it is about to extend.
        var available = basePool.Where(q => !lockedSet.Contains(q.Id) || IsOwnMatchReservation(q.Id, request)).ToList();

        var withTopicFilter = ApplyTopicFilter(available, topicFilter);
        var eligibleAfterFilters = withTopicFilter.Count;

        var usageExcluded = await GetRepeatPolicyExclusionsAsync(request, repeatPolicy, cancellationToken);
        var afterRepeatPolicy = withTopicFilter.Where(q => !usageExcluded.Contains(q.Id)).ToList();
        var eligibleAfterRepeatPolicy = afterRepeatPolicy.Count;

        var withinDifficulty = ApplyDifficultyBounds(afterRepeatPolicy, minDifficulty, maxDifficulty);

        var rng = CreateRandom(request.RandomSeed);
        var usedTopicsInDraw = new HashSet<Guid>();
        var selected = DrawByMix(withinDifficulty, difficultyMixRequested, request.QuestionCount, topicSpreadPolicy, rng, usedTopicsInDraw);

        var warnings = new List<string>();

        if (selected.Count < request.QuestionCount && fallbackPolicy == FallbackPolicy.WidenThenFail)
        {
            // Step 1: widen difficulty by one level in each direction.
            var widenedMin = Widen(minDifficulty, -1);
            var widenedMax = Widen(maxDifficulty, 1);
            var widenedPool = ApplyDifficultyBounds(afterRepeatPolicy, widenedMin, widenedMax)
                .Where(q => selected.All(s => s.Id != q.Id)).ToList();
            selected.AddRange(DrawMore(widenedPool, request.QuestionCount - selected.Count, topicSpreadPolicy, rng, usedTopicsInDraw));
            if (selected.Count > 0)
            {
                warnings.Add($"Widened difficulty range to {widenedMin}-{widenedMax} to reach the requested count.");
            }

            // Step 2: drop the topic filter.
            if (selected.Count < request.QuestionCount && topicFilter is not null)
            {
                var noTopicPool = ApplyDifficultyBounds(
                        available.Where(q => !usageExcluded.Contains(q.Id)).ToList(), widenedMin, widenedMax)
                    .Where(q => selected.All(s => s.Id != q.Id)).ToList();
                selected.AddRange(DrawMore(noTopicPool, request.QuestionCount - selected.Count, topicSpreadPolicy, rng, usedTopicsInDraw));
                if (selected.Count > 0)
                {
                    warnings.Add("Dropped the topic filter to reach the requested count.");
                }
            }

            // Step 3: allow repeats the repeat policy would otherwise forbid.
            if (selected.Count < request.QuestionCount)
            {
                var anyRepeatPool = ApplyDifficultyBounds(available, widenedMin, widenedMax)
                    .Where(q => selected.All(s => s.Id != q.Id)).ToList();
                selected.AddRange(DrawMore(anyRepeatPool, request.QuestionCount - selected.Count, topicSpreadPolicy, rng, usedTopicsInDraw));
                if (selected.Count > 0)
                {
                    warnings.Add("Allowed a previously-used question to reach the requested count — the repeat policy could not be honoured.");
                }
            }
        }

        var achievedMix = selected
            .GroupBy(q => q.DifficultyLevel)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        if (selected.Count < request.QuestionCount)
        {
            var message = $"Not enough {request.FormatCode} questions available for stage '{request.StageId}': " +
                $"required {request.QuestionCount}, available {selected.Count} (difficulty range {minDifficulty}-{maxDifficulty}).";

            if (throwOnExhaustion)
            {
                throw new QuestionPoolExhaustedException(message);
            }

            warnings.Add(message);
        }

        return new DrawOutcome(
            selected, basePool.Count, eligibleAfterFilters, eligibleAfterRepeatPolicy,
            difficultyMixRequested.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value), achievedMix, warnings);
    }

    /// <summary>A question already Reserved/Active for this same match is not
    /// a conflict — it is the caller's own earlier segment. Only cross-match
    /// locks should exclude a candidate.</summary>
    private bool IsOwnMatchReservation(Guid questionId, SelectionRequest request)
    {
        if (request.MatchId is null)
        {
            return false;
        }

        return _db.MatchQuestions.Local
            .Any(mq => mq.QuestionId == questionId && mq.MatchId == request.MatchId && mq.State != MatchQuestionState.Released);
    }

    private static List<Question> ApplyTopicFilter(List<Question> pool, HashSet<Guid>? topicFilter) =>
        topicFilter is null ? pool : pool.Where(q => q.TopicId is not null && topicFilter.Contains(q.TopicId.Value)).ToList();

    private static List<Question> ApplyDifficultyBounds(List<Question> pool, DifficultyLevel min, DifficultyLevel max) =>
        pool.Where(q => q.DifficultyLevel >= min && q.DifficultyLevel <= max).ToList();

    private async Task<HashSet<Guid>> GetRepeatPolicyExclusionsAsync(
        SelectionRequest request, RepeatPolicy repeatPolicy, CancellationToken cancellationToken)
    {
        IQueryable<QuestionUsageHistory> query = _db.QuestionUsageHistories
            .Where(h => h.ProgramId == request.ProgramId);

        query = repeatPolicy switch
        {
            // A preview has no MatchId yet — nothing has been served for a
            // match that does not exist, so this excludes nothing.
            RepeatPolicy.NeverInMatch => request.MatchId is null
                ? query.Where(_ => false)
                : query.Where(h => h.MatchId == request.MatchId),
            RepeatPolicy.NeverInStage => query.Where(h => h.StageId == request.StageId),
            RepeatPolicy.NeverInProgram => query,
            RepeatPolicy.NeverForTeam => request.TeamId is null
                ? query.Where(_ => false)
                : query.Where(h => h.TeamId == request.TeamId),
            _ => query,
        };

        var ids = await query.Select(h => h.QuestionId).Distinct().ToListAsync(cancellationToken);
        return new HashSet<Guid>(ids);
    }

    /// <summary>P8-03/P8-04: splits the request across the configured
    /// difficulty mix (each bucket drawn independently, so the mix is honoured
    /// as closely as that bucket's own pool allows) and falls back to one
    /// unconstrained weighted draw when no mix is configured.</summary>
    private static List<Question> DrawByMix(
        List<Question> pool, IReadOnlyDictionary<DifficultyLevel, int> mix, int questionCount,
        TopicSpreadPolicy topicSpreadPolicy, Random rng, HashSet<Guid> usedTopicsInDraw)
    {
        if (mix.Count == 0)
        {
            return DrawMore(pool, questionCount, topicSpreadPolicy, rng, usedTopicsInDraw);
        }

        var selected = new List<Question>();
        foreach (var (difficulty, count) in mix)
        {
            if (count <= 0)
            {
                continue;
            }

            var bucket = pool.Where(q => q.DifficultyLevel == difficulty && selected.All(s => s.Id != q.Id)).ToList();
            selected.AddRange(DrawMore(bucket, count, topicSpreadPolicy, rng, usedTopicsInDraw));
        }

        return selected;
    }

    /// <summary>Seeded weighted random draw without replacement, weighting
    /// toward lower <c>TimesUsed</c> (P8-04) with an optional preference for
    /// an unused topic within this draw (P8-05, TopicSpreadPolicy.OnePerTopicIfPossible).
    /// The same seed always produces the same draw because the candidate pool
    /// is sorted by Id before it is consulted.</summary>
    private static List<Question> DrawMore(
        List<Question> candidates, int count, TopicSpreadPolicy topicSpreadPolicy, Random rng, HashSet<Guid> usedTopicsInDraw)
    {
        var pool = candidates.OrderBy(q => q.Id).ToList();
        var picked = new List<Question>();

        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var candidates2 = pool;
            if (topicSpreadPolicy == TopicSpreadPolicy.OnePerTopicIfPossible)
            {
                var unusedTopic = pool.Where(q => q.TopicId is null || !usedTopicsInDraw.Contains(q.TopicId.Value)).ToList();
                if (unusedTopic.Count > 0)
                {
                    candidates2 = unusedTopic;
                }
            }

            var weights = candidates2.Select(q => 1.0 / (1 + q.TimesUsed)).ToList();
            var totalWeight = weights.Sum();
            var roll = rng.NextDouble() * totalWeight;
            var cumulative = 0.0;
            var chosen = candidates2[^1];
            for (var j = 0; j < candidates2.Count; j++)
            {
                cumulative += weights[j];
                if (roll <= cumulative)
                {
                    chosen = candidates2[j];
                    break;
                }
            }

            picked.Add(chosen);
            pool.Remove(chosen);
            if (chosen.TopicId is not null)
            {
                usedTopicsInDraw.Add(chosen.TopicId.Value);
            }
        }

        return picked;
    }

    private static DifficultyLevel Widen(DifficultyLevel level, int steps)
    {
        var value = (int)level + steps;
        return (DifficultyLevel)Math.Clamp(value, (int)DifficultyLevel.VeryEasy, (int)DifficultyLevel.VeryHard);
    }

    private static Random CreateRandom(long seed) => new(unchecked((int)(seed ^ (seed >> 32))));

    private static HashSet<Guid>? ParseGuidSet(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var ids = JsonSerializer.Deserialize<List<Guid>>(json);
        return ids is null || ids.Count == 0 ? null : new HashSet<Guid>(ids);
    }

    /// <summary>DifficultyMixJson is a percentage map, e.g.
    /// <c>{"Easy":60,"Hard":40}</c>. Percentages are converted to a target
    /// count per difficulty that sums to exactly <paramref name="questionCount"/>
    /// (the last bucket absorbs any rounding remainder). An absent or empty
    /// map means "no mix constraint" — draw purely by weight.</summary>
    private static Dictionary<DifficultyLevel, int> ParseDifficultyMix(string? mixJson, int questionCount)
    {
        if (string.IsNullOrWhiteSpace(mixJson))
        {
            return new Dictionary<DifficultyLevel, int>();
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(mixJson) ?? new Dictionary<string, int>();
        var parsed = raw
            .Where(kv => Enum.TryParse<DifficultyLevel>(kv.Key, ignoreCase: true, out _) && kv.Value > 0)
            .ToDictionary(kv => Enum.Parse<DifficultyLevel>(kv.Key, ignoreCase: true), kv => kv.Value);

        var totalWeight = parsed.Values.Sum();
        if (totalWeight <= 0)
        {
            return new Dictionary<DifficultyLevel, int>();
        }

        var result = new Dictionary<DifficultyLevel, int>();
        var assigned = 0;
        var keys = parsed.Keys.ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var count = i == keys.Count - 1
                ? questionCount - assigned
                : (int)Math.Round(questionCount * (parsed[keys[i]] / (double)totalWeight), MidpointRounding.AwayFromZero);
            assigned += count;
            result[keys[i]] = Math.Max(0, count);
        }

        return result;
    }

    private sealed record DrawOutcome(
        List<Question> Selected,
        int PoolSize,
        int EligibleAfterFilters,
        int EligibleAfterRepeatPolicy,
        IReadOnlyDictionary<string, int> DifficultyMixRequested,
        IReadOnlyDictionary<string, int> DifficultyMixAchieved,
        IReadOnlyList<string> Warnings);
}
