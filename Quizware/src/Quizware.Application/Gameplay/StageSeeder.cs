using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Commands;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Application.Gameplay;

/// <summary>Creates a stage's matches from a ranked list of teams (best
/// first), grouped per <see cref="MatchSeeding"/> within the stage's match
/// sizes. Each match is built exactly like a manual one — segments from the
/// stage templates, seats and turn order in group order. Shared by auto-seed
/// (P9-01) and the qualification commit (P11-07). Does not save.</summary>
public sealed class StageSeeder
{
    private readonly IAppDbContext _db;

    public StageSeeder(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<int> SeedAsync(
        Guid programId, Stage stage, IReadOnlyList<Guid> rankedTeams, SeedingMode mode, string actor, CancellationToken cancellationToken)
    {
        var groups = MatchSeeding.Group(
            rankedTeams, Math.Max(2, stage.MinTeamsPerMatch), stage.MaxTeamsPerMatch, mode, new Random(Random.Shared.Next()));

        var templates = await _db.StageSegmentTemplates.Where(t => t.StageId == stage.Id).ToListAsync(cancellationToken);
        var nextNumber = (await _db.Matches.Where(m => m.StageId == stage.Id).Select(m => (int?)m.MatchNumber).MaxAsync(cancellationToken) ?? 0) + 1;

        foreach (var group in groups)
        {
            var match = Match.Create(
                programId, stage.Id, nextNumber, Random.Shared.NextInt64(1, long.MaxValue), actor, name: $"Match {nextNumber}");
            nextNumber++;
            _db.Matches.Add(match);
            _db.MatchSegments.AddRange(CreateMatchCommandHandler.InstantiateSegments(match, stage, templates, actor));
            for (var i = 0; i < group.Count; i++)
            {
                _db.MatchParticipants.Add(MatchParticipant.Create(programId, match.Id, group[i], i + 1, i + 1, actor));
            }
        }

        return groups.Count;
    }
}
