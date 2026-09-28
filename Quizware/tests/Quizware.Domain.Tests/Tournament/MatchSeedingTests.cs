using FluentAssertions;
using Quizware.Domain.Common.Exceptions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Domain.Tests.Tournament;

public class MatchSeedingTests
{
    private static readonly IReadOnlyList<Guid> Nine = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToList();

    private static IEnumerable<IEnumerable<int>> Ranks(IReadOnlyList<IReadOnlyList<Guid>> groups, IReadOnlyList<Guid> ranked) =>
        groups.Select(g => g.Select(id => ranked.ToList().IndexOf(id) + 1));

    [Fact]
    public void ByRank_KeepsNeighboursTogether()
    {
        var groups = MatchSeeding.Group(Nine, 2, 3, SeedingMode.ByRank, new Random(1));

        Ranks(groups, Nine).Should().BeEquivalentTo(new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Snake_DealsBackAndForth()
    {
        var groups = MatchSeeding.Group(Nine, 2, 3, SeedingMode.Snake, new Random(1));

        Ranks(groups, Nine).Should().BeEquivalentTo(new[] { new[] { 1, 6, 7 }, new[] { 2, 5, 8 }, new[] { 3, 4, 9 } }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Random_PlacesEveryTeamExactlyOnce()
    {
        var groups = MatchSeeding.Group(Nine, 2, 3, SeedingMode.Random, new Random(7));

        groups.SelectMany(g => g).Should().BeEquivalentTo(Nine);
        groups.Should().OnlyContain(g => g.Count == 3);
    }

    [Fact]
    public void UnevenCounts_AreSplitAsEvenlyAsPossible()
    {
        var seven = Nine.Take(7).ToList();

        var groups = MatchSeeding.Group(seven, 2, 3, SeedingMode.ByRank, new Random(1));

        groups.Select(g => g.Count).Should().Equal(3, 2, 2);
    }

    [Fact]
    public void TooFewTeams_OrAnImpossibleSplit_Throws()
    {
        var tooFew = () => MatchSeeding.Group(Nine.Take(1).ToList(), 2, 3, SeedingMode.ByRank, new Random(1));
        var impossible = () => MatchSeeding.Group(Nine.Take(4).ToList(), 3, 3, SeedingMode.ByRank, new Random(1));
        var manual = () => MatchSeeding.Group(Nine, 2, 3, SeedingMode.Manual, new Random(1));

        tooFew.Should().Throw<InsufficientParticipantsException>();
        impossible.Should().Throw<InsufficientParticipantsException>();
        manual.Should().Throw<ArgumentException>();
    }
}
