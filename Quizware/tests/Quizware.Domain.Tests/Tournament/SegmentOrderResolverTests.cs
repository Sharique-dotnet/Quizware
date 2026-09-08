using FluentAssertions;
using Quizware.Domain.Enums;
using Quizware.Domain.Tournament;

namespace Quizware.Domain.Tests.Tournament;

public class SegmentOrderResolverTests
{
    [Fact]
    public void Resolve_FixedMode_ReturnsTemplateOrder()
    {
        var s1 = new SegmentOrderInput(Guid.NewGuid(), TemplateOrderIndex: 1, IsOrderLocked: false);
        var s2 = new SegmentOrderInput(Guid.NewGuid(), TemplateOrderIndex: 2, IsOrderLocked: false);
        var s3 = new SegmentOrderInput(Guid.NewGuid(), TemplateOrderIndex: 3, IsOrderLocked: false);
        var segments = new[] { s3, s1, s2 };

        var result = SegmentOrderResolver.Resolve(segments, SegmentOrderMode.Fixed, seed: 123);

        result.Should().Equal(s1.SegmentId, s2.SegmentId, s3.SegmentId);
    }

    [Fact]
    public void Resolve_RandomPerMatch_SameSeedProducesSameOrder()
    {
        var segments = Enumerable.Range(1, 5)
            .Select(i => new SegmentOrderInput(Guid.NewGuid(), i, IsOrderLocked: false))
            .ToArray();

        var first = SegmentOrderResolver.Resolve(segments, SegmentOrderMode.RandomPerMatch, seed: 987654321);
        var second = SegmentOrderResolver.Resolve(segments, SegmentOrderMode.RandomPerMatch, seed: 987654321);

        first.Should().Equal(second);
    }

    [Fact]
    public void Resolve_RandomPerMatch_LockedSegmentKeepsItsTemplatePosition()
    {
        var locked = new SegmentOrderInput(Guid.NewGuid(), TemplateOrderIndex: 3, IsOrderLocked: true);
        var segments = new[]
        {
            new SegmentOrderInput(Guid.NewGuid(), 1, false),
            new SegmentOrderInput(Guid.NewGuid(), 2, false),
            locked,
            new SegmentOrderInput(Guid.NewGuid(), 4, false),
        };

        var result = SegmentOrderResolver.Resolve(segments, SegmentOrderMode.RandomPerMatch, seed: 55);

        result[2].Should().Be(locked.SegmentId);
    }
}
