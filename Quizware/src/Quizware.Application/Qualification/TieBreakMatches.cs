using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Qualification;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Qualification;

/// <summary>P11-05/06: what a tie-break match does to its tie. On completion
/// the still-contested teams are ranked by their tie-break points: whole
/// point levels that fit in the open slots qualify, levels below them are
/// out, and a level straddling the last slot stays contested. If nothing
/// straddles, the tie is resolved as PlayedTieBreak; otherwise the round
/// counts as played level and the tie waits for another round — until
/// <c>MaxExtraRounds</c> is used up, when the rule's <c>OnStillTied</c>
/// applies (ShareTheSlot is applied at once; ManualDecision and CoinToss
/// wait for <c>resolve-manually</c>). An abandoned or deleted tie-break
/// match sends its tie back to waiting for one. Callers own the unit of work.</summary>
public sealed class TieBreakMatches
{
    public const string TieBreakResolved = LiveEvents.TieBreakResolved;

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IMatchNotifications _notifications;

    public TieBreakMatches(IAppDbContext db, ICurrentUser currentUser, IMatchNotifications notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task RecordResultAsync(Match match, IReadOnlyList<MatchParticipant> participants, CancellationToken cancellationToken)
    {
        var tie = await LinkedTieAsync(match, cancellationToken);
        if (tie is null)
        {
            return;
        }

        var rule = await _db.TieBreakRules.IgnoreQueryFilters().SingleAsync(r => r.Id == tie.TieBreakRuleId, cancellationToken);
        var teams = await _db.TieBreakParticipants.Where(p => p.TieBreakEventId == tie.Id).ToListAsync(cancellationToken);
        var alreadyIn = teams.Count(t => !t.IsContested && t.Qualified);
        var slots = tie.ContestedSlots - alreadyIn;

        // A team disqualified from the tie-break, or missing from it, finishes behind everyone who played it out.
        var points = participants
            .Where(p => p.Status == ParticipantStatus.Active && !p.ExcludeFromStandings)
            .ToDictionary(p => p.TeamId, p => p.FinalScore);
        var levels = teams
            .Where(t => t.IsContested)
            .GroupBy(t => points.TryGetValue(t.TeamId, out var p) ? p : int.MinValue)
            .OrderByDescending(g => g.Key)
            .ToList();

        var position = alreadyIn + 1;
        var straddling = new List<TieBreakParticipant>();
        var straddlingRank = 0;
        foreach (var level in levels)
        {
            var group = level.ToList();
            var remaining = slots - (position - alreadyIn - 1);
            var score = level.Key == int.MinValue ? 0 : level.Key;
            if (straddling.Count == 0 && remaining > 0 && group.Count > remaining)
            {
                straddling = group;
                straddlingRank = position;
                group.ForEach(t => t.RecordPoints(score));
            }
            else
            {
                var qualified = straddling.Count == 0 && remaining > 0;
                group.ForEach(t => t.RecordResult(score, position, qualified));
            }

            position += group.Count;
        }

        var resolvedBy = _currentUser.UserId ?? Guid.Empty;
        if (straddling.Count == 0)
        {
            tie.Resolve(TieBreakResolutionMethod.PlayedTieBreak, resolvedBy);
        }
        else
        {
            tie.RecordExtraRound();
            tie.CancelTieBreakMatch();
            if (!TieViews.MayPlayAnotherRound(tie, rule) && rule.OnStillTied == OnStillTiedPolicy.ShareTheSlot)
            {
                straddling.ForEach(t => t.RecordResult(t.TieBreakPoints, straddlingRank, qualified: true));
                tie.Resolve(TieBreakResolutionMethod.Shared, resolvedBy,
                    notes: $"Still level after {tie.RoundsPlayed} tie-break round(s); the rule shares the slot.");
            }
        }

        _notifications.Publish(TieBreakResolved, match.ProgramId, match.Id, new
        {
            tieBreakEventId = tie.Id,
            state = tie.State.ToString(),
            resolutionMethod = tie.ResolutionMethod?.ToString(),
            roundsPlayed = tie.RoundsPlayed,
            results = teams.Select(t => new { teamId = t.TeamId, points = t.TieBreakPoints, rank = t.ResultRank, qualified = t.Qualified }),
        });
    }

    /// <summary>The match was abandoned or deleted before deciding anything.</summary>
    public async Task ReleaseAsync(Match match, CancellationToken cancellationToken)
    {
        var tie = await LinkedTieAsync(match, cancellationToken);
        tie?.CancelTieBreakMatch();
    }

    /// <summary>Before a tie is settled by hand or abandoned: a tie-break match
    /// that has not started is deleted; one being played must be finished or
    /// abandoned first.</summary>
    public async Task WithdrawMatchAsync(TieBreakEvent tie, string actor, CancellationToken cancellationToken)
    {
        if (tie.State != TieBreakEventState.InProgress || tie.TieBreakMatchId is not { } matchId)
        {
            return;
        }

        var match = await _db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, cancellationToken);
        if (match is not null && !match.IsInSetup)
        {
            throw new InvalidStateTransitionException(
                $"Tie-break match {match.MatchNumber} is {match.State}; finish or abandon it before settling this tie another way.");
        }

        if (match is not null)
        {
            match.Delete(actor);
            foreach (var participant in await MatchSetup.LoadParticipantsAsync(_db, match.Id, cancellationToken))
            {
                participant.Delete(actor);
            }

            foreach (var segment in await MatchSetup.LoadSegmentsAsync(_db, match.Id, cancellationToken))
            {
                segment.Delete(actor);
            }
        }

        tie.CancelTieBreakMatch();
    }

    private async Task<TieBreakEvent?> LinkedTieAsync(Match match, CancellationToken cancellationToken)
    {
        if (match.MatchKind != MatchKind.TieBreak || match.TieBreakEventId is not { } tieId)
        {
            return null;
        }

        var tie = await _db.TieBreakEvents.SingleOrDefaultAsync(e => e.Id == tieId, cancellationToken);
        return tie is { State: TieBreakEventState.InProgress } && tie.TieBreakMatchId == match.Id ? tie : null;
    }
}
