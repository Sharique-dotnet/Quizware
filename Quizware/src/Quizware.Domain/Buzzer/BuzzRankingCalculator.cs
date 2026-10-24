namespace Quizware.Domain.Buzzer;

public sealed record BuzzPressReading(Guid ParticipantId, int PressTimeMs);

public sealed record BuzzRanking(Guid ParticipantId, int Rank, int PressTimeMs);

/// <summary>Ported from QuickBuzz's DeviceApiController: lowest non-zero press
/// time wins; 0 means "no press" and always sorts last.</summary>
public static class BuzzRankingCalculator
{
    public static IReadOnlyList<BuzzRanking> Rank(IReadOnlyList<BuzzPressReading> readings) =>
        Rank(readings, r => r.PressTimeMs)
            .Select(r => new BuzzRanking(r.Item.ParticipantId, r.Rank, r.Item.PressTimeMs))
            .ToList();

    /// <summary>The same rule for any press record: ranks 1..n in press order,
    /// "no press" (0) last; equal times keep their incoming order.</summary>
    public static IReadOnlyList<(T Item, int Rank)> Rank<T>(IEnumerable<T> items, Func<T, int> pressTimeMs) =>
        items
            .OrderBy(i => pressTimeMs(i) == 0 ? long.MaxValue : pressTimeMs(i))
            .Select((item, index) => (item, index + 1))
            .ToList();
}
