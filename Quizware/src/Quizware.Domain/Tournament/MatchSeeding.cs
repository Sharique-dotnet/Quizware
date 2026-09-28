using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;

namespace Quizware.Domain.Tournament;

/// <summary>Splits a stage's teams into matches (P9-01). Match sizes are as
/// even as possible within the stage's limits: the fewest matches that keep
/// every match at or under the maximum, larger matches first. Given teams
/// ranked best first — ByRank keeps neighbours together (1-2-3, 4-5-6),
/// Snake deals them back and forth so each match gets a spread of strength
/// (1-6-7, 2-5-8, 3-4-9), Random shuffles before filling.</summary>
public static class MatchSeeding
{
    public static IReadOnlyList<IReadOnlyList<Guid>> Group(
        IReadOnlyList<Guid> rankedTeams, int minPerMatch, int maxPerMatch, SeedingMode mode, Random random)
    {
        if (mode == SeedingMode.Manual)
        {
            throw new ArgumentException("Manual seeding has no automatic grouping.", nameof(mode));
        }

        var count = rankedTeams.Count;
        if (count < minPerMatch)
        {
            throw new InsufficientParticipantsException(
                $"{count} team(s) cannot fill a match of at least {minPerMatch}.");
        }

        var matchCount = (count + maxPerMatch - 1) / maxPerMatch;
        var sizes = Enumerable.Range(0, matchCount).Select(i => count / matchCount + (i < count % matchCount ? 1 : 0)).ToList();
        if (sizes.Min() < minPerMatch)
        {
            throw new InsufficientParticipantsException(
                $"{count} teams cannot be split into matches of {minPerMatch}-{maxPerMatch} teams.");
        }

        var groups = sizes.Select(_ => new List<Guid>()).ToList();
        var teams = mode == SeedingMode.Random ? rankedTeams.OrderBy(_ => random.Next()).ToList() : rankedTeams.ToList();

        if (mode == SeedingMode.Snake)
        {
            var forward = true;
            var index = 0;
            while (index < teams.Count)
            {
                var order = forward ? Enumerable.Range(0, matchCount) : Enumerable.Range(0, matchCount).Reverse();
                foreach (var g in order.Where(g => groups[g].Count < sizes[g]))
                {
                    if (index < teams.Count)
                    {
                        groups[g].Add(teams[index++]);
                    }
                }

                forward = !forward;
            }
        }
        else
        {
            var index = 0;
            for (var g = 0; g < matchCount; g++)
            {
                groups[g].AddRange(teams.Skip(index).Take(sizes[g]));
                index += sizes[g];
            }
        }

        return groups;
    }
}
