using QuizApp.Domain.Enums;

namespace QuizApp.Domain.Tournament;

public sealed record SegmentOrderInput(Guid SegmentId, int TemplateOrderIndex, bool IsOrderLocked);

/// <summary>Resolves the order segments play in: template order, optionally
/// shuffled per match by the match's stored seed, always honouring locked
/// segments (they never move). The same seed always produces the same order.</summary>
public static class SegmentOrderResolver
{
    public static IReadOnlyList<Guid> Resolve(IReadOnlyList<SegmentOrderInput> segments, SegmentOrderMode mode, long seed)
    {
        var byTemplateOrder = segments.OrderBy(s => s.TemplateOrderIndex).ToList();

        if (mode != SegmentOrderMode.RandomPerMatch)
        {
            return byTemplateOrder.Select(s => s.SegmentId).ToList();
        }

        var shuffledUnlocked = new Queue<SegmentOrderInput>(
            Shuffle(byTemplateOrder.Where(s => !s.IsOrderLocked).ToList(), seed));
        var locked = new Queue<SegmentOrderInput>(byTemplateOrder.Where(s => s.IsOrderLocked));

        return byTemplateOrder
            .Select(slot => (slot.IsOrderLocked ? locked.Dequeue() : shuffledUnlocked.Dequeue()).SegmentId)
            .ToList();
    }

    private static List<SegmentOrderInput> Shuffle(List<SegmentOrderInput> items, long seed)
    {
        var rng = new Random(unchecked((int)seed));
        var copy = new List<SegmentOrderInput>(items);

        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }
}
