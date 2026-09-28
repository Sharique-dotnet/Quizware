using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Scoring.Dtos;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Scoring;

/// <summary>A match's scoreboard straight from TeamMatchScore — the read
/// model, never a recount of answers. Standard competition ranking (1, 1, 3).</summary>
internal static class MatchScoreboard
{
    public static async Task<IReadOnlyList<TeamScoreItemDto>> BuildAsync(
        IAppDbContext db, Match match, CancellationToken cancellationToken)
    {
        var scores = await db.TeamMatchScores.Where(s => s.MatchId == match.Id).ToListAsync(cancellationToken);
        var teamIds = scores.Select(s => s.TeamId).ToList();
        var names = await db.Teams.IgnoreQueryFilters()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DisplayName, cancellationToken);

        var ordered = scores.OrderByDescending(s => s.TotalPoints).ToList();
        var board = new List<TeamScoreItemDto>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].TotalPoints == ordered[i - 1].TotalPoints ? board[i - 1].Rank : i + 1;
            board.Add(new TeamScoreItemDto(ordered[i].TeamId, names.GetValueOrDefault(ordered[i].TeamId, string.Empty), ordered[i].TotalPoints, rank));
        }

        return board;
    }

    public static async Task<Match> LoadMatchAsync(IAppDbContext db, Guid matchId, CancellationToken cancellationToken) =>
        await db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, cancellationToken)
        ?? throw new KeyNotFoundException($"Match '{matchId}' was not found.");
}
