using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Qualification.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;

namespace Quizware.Application.Qualification;

/// <summary>Builds the API view of saved ties: who is tied, what every
/// criterion made of them (re-run live, so the values always match the
/// stored scores), the rule that governs the tie, and what can be done next.</summary>
public sealed class TieViews
{
    public const string CreateTieBreakMatch = "CreateTieBreakMatch";
    public const string ResolveManually = "ResolveManually";
    public const string Abandon = "Abandon";

    private readonly IAppDbContext _db;
    private readonly ITieBreakCriteriaService _criteria;

    public TieViews(IAppDbContext db, ITieBreakCriteriaService criteria)
    {
        _db = db;
        _criteria = criteria;
    }

    public async Task<StageTiesDto> ForStageAsync(Guid programId, Guid stageId, CancellationToken cancellationToken)
    {
        if (!await _db.Stages.AnyAsync(s => s.Id == stageId && s.ProgramId == programId, cancellationToken))
        {
            throw new KeyNotFoundException($"Stage '{stageId}' was not found.");
        }

        var events = await _db.TieBreakEvents
            .Where(e => e.StageId == stageId && e.State != TieBreakEventState.Abandoned)
            .OrderBy(e => e.DetectedAtUtc)
            .ToListAsync(cancellationToken);
        var ties = new List<TieEntryDto>();
        foreach (var tie in events)
        {
            ties.Add(await BuildAsync(tie, cancellationToken));
        }

        return new StageTiesDto(stageId, ties, ties.Any(t => t.AffectsQualification && IsOpen(t.State)));
    }

    public async Task<TieEntryDto> ForTieAsync(Guid programId, Guid tieId, CancellationToken cancellationToken) =>
        await BuildAsync(await LoadAsync(_db, programId, tieId, cancellationToken), cancellationToken);

    public static async Task<TieBreakEvent> LoadAsync(IAppDbContext db, Guid programId, Guid tieId, CancellationToken cancellationToken) =>
        await db.TieBreakEvents.SingleOrDefaultAsync(e => e.Id == tieId && e.ProgramId == programId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tie '{tieId}' was not found.");

    public static bool AffectsQualification(TieBreakEvent tie, int teamCount) => tie.ContestedSlots > 0 && tie.ContestedSlots < teamCount;

    /// <summary>A tie-break match may be played for the initial round and up to
    /// <c>MaxExtraRounds</c> more; <see cref="TieBreakEvent.RoundsPlayed"/> counts the rounds that ended level.</summary>
    public static bool MayPlayAnotherRound(TieBreakEvent tie, TieBreakRule rule) =>
        rule.PlayTieBreakSegment && tie.RoundsPlayed <= rule.MaxExtraRounds;

    public async Task<TieEntryDto> BuildAsync(TieBreakEvent tie, CancellationToken cancellationToken)
    {
        var participants = await _db.TieBreakParticipants
            .Where(p => p.TieBreakEventId == tie.Id)
            .OrderBy(p => p.ResultRank ?? int.MaxValue)
            .ThenBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var teamIds = participants.Select(p => p.TeamId).ToList();
        var names = await _db.Teams.IgnoreQueryFilters()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);
        var rule = await _db.TieBreakRules.IgnoreQueryFilters().SingleAsync(r => r.Id == tie.TieBreakRuleId, cancellationToken);
        var ordering = await _criteria.OrderTiedTeamsAsync(tie.StageId, teamIds, cancellationToken);
        var affects = AffectsQualification(tie, participants.Count);

        var actions = new List<string>();
        if (affects && tie.IsOpen && tie.State != TieBreakEventState.InProgress)
        {
            if (MayPlayAnotherRound(tie, rule))
            {
                actions.Add(CreateTieBreakMatch);
            }

            actions.Add(ResolveManually);
            actions.Add(Abandon);
        }

        return new TieEntryDto(
            tie.Id,
            tie.State.ToString(),
            tie.ContestedRank,
            tie.ContestedSlots,
            affects,
            participants.Select(p => new TieTeamEntryDto(p.TeamId, names.GetValueOrDefault(p.TeamId, string.Empty), p.EnteringScore)).ToList(),
            ordering.Trace
                .Select(c => new TieCriterionEntryDto(
                    c.Criterion,
                    c.Separated,
                    c.Note is not null && !c.Separated ? null : teamIds.Select(id => Display(c.Criterion, c.Values.GetValueOrDefault(id))).ToList(),
                    c.Note))
                .ToList(),
            new TieBreakRuleSummaryDto(
                rule.TieBreakFormatCode.ToString(),
                rule.QuestionCount,
                [(int)rule.MinDifficultyLevel, (int)rule.MaxDifficultyLevel],
                rule.SuddenDeath,
                rule.MaxExtraRounds,
                rule.ScoreCountsTowardStage,
                rule.OnStillTied.ToString()),
            actions);
    }

    private static bool IsOpen(string state) =>
        state is nameof(TieBreakEventState.Detected) or nameof(TieBreakEventState.AwaitingPlay) or nameof(TieBreakEventState.InProgress);

    /// <summary>Criteria values are stored higher-is-better; the two
    /// "fewer/faster" criteria are shown as the raw count or milliseconds.</summary>
    private static int Display(string criterion, decimal value) => criterion switch
    {
        TieBreakCriteriaService.FewerIncorrect or TieBreakCriteriaService.FasterAverageBuzzTime => (int)Math.Round(-value),
        _ => (int)Math.Round(value),
    };
}
