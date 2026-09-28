using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Gameplay;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Loading and mapping shared by the match setup and live handlers.</summary>
internal static class MatchSetup
{
    public static string DisplayName(Match match) => match.Name ?? $"Match {match.MatchNumber}";

    public static async Task<Match> LoadMatchAsync(
        IAppDbContext db, Guid? programId, Guid matchId, CancellationToken cancellationToken)
    {
        return await db.Matches
            .SingleOrDefaultAsync(m => m.Id == matchId && (programId == null || m.ProgramId == programId), cancellationToken)
            ?? throw new KeyNotFoundException($"Match '{matchId}' was not found.");
    }

    public static Task<List<MatchParticipant>> LoadParticipantsAsync(
        IAppDbContext db, Guid matchId, CancellationToken cancellationToken) =>
        db.MatchParticipants.Where(p => p.MatchId == matchId).OrderBy(p => p.SeatNumber).ToListAsync(cancellationToken);

    public static Task<List<MatchSegment>> LoadSegmentsAsync(
        IAppDbContext db, Guid matchId, CancellationToken cancellationToken) =>
        db.MatchSegments.Where(s => s.MatchId == matchId).OrderBy(s => s.OrderIndex).ToListAsync(cancellationToken);

    public static async Task<MatchDto> ToDetailAsync(IAppDbContext db, Match match, CancellationToken cancellationToken)
    {
        var participants = await LoadParticipantsAsync(db, match.Id, cancellationToken);
        var teamNames = await TeamNamesAsync(db, participants.Select(p => p.TeamId), cancellationToken);

        return new MatchDto(
            match.Id,
            DisplayName(match),
            match.StageId,
            match.State.ToString(),
            match.MatchNumber,
            match.MatchKind.ToString(),
            participants.Select(p => ToDto(p, teamNames)).ToList());
    }

    public static ParticipantDto ToDto(MatchParticipant participant, IReadOnlyDictionary<Guid, string> teamNames) =>
        new(
            participant.Id,
            participant.TeamId,
            teamNames.GetValueOrDefault(participant.TeamId, string.Empty),
            participant.SeatNumber,
            participant.TurnOrder,
            participant.Status.ToString());

    public static MatchSegmentDto ToDto(MatchSegment segment) =>
        new(segment.Id, segment.FormatCode.ToString(), segment.OrderIndex, segment.PlannedQuestionCount,
            segment.State.ToString(), segment.IsOrderLocked);

    public static async Task<Dictionary<Guid, string>> TeamNamesAsync(
        IAppDbContext db, IEnumerable<Guid> teamIds, CancellationToken cancellationToken)
    {
        var ids = teamIds.Distinct().ToList();
        return await db.Teams
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);
    }

    /// <summary>Two-phase reindex (D-023): the unique (MatchId, OrderIndex)
    /// index would otherwise be violated mid-batch depending on the order EF
    /// writes rows in. <paramref name="finalOrder"/> maps each segment to its
    /// final index.</summary>
    public static async Task ApplySegmentOrderAsync(
        IAppDbContext db, IReadOnlyList<(MatchSegment Segment, int OrderIndex)> finalOrder, CancellationToken cancellationToken)
    {
        var changed = finalOrder.Where(x => x.Segment.OrderIndex != x.OrderIndex).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        for (var i = 0; i < changed.Count; i++)
        {
            changed[i].Segment.Renumber(100_000 + i);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (segment, orderIndex) in changed)
        {
            segment.Renumber(orderIndex);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Renumbers every remaining active participant's TurnOrder to
    /// 1..N, keeping relative order (TurnOrderCalculator.Recompact).</summary>
    public static void RecompactTurnOrder(IReadOnlyList<MatchParticipant> participants)
    {
        var byId = participants.ToDictionary(p => p.Id);
        foreach (var assignment in TurnOrderCalculator.Recompact(participants))
        {
            byId[assignment.ParticipantId].ApplyTurnOrder(assignment.TurnOrder);
        }
    }
}
