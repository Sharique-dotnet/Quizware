namespace Quizware.Domain.Buzzer;

public sealed record BuzzPressReading(Guid ParticipantId, int PressTimeMs);

public sealed record BuzzRanking(Guid ParticipantId, int Rank, int PressTimeMs);

/// <summary>Ported from QuickBuzz's DeviceApiController: lowest non-zero press
/// time wins; 0 means "no press" and always sorts last.</summary>
public static class BuzzRankingCalculator
{
    public static IReadOnlyList<BuzzRanking> Rank(IReadOnlyList<BuzzPressReading> readings)
    {
        return readings
            .OrderBy(r => r.PressTimeMs == 0 ? long.MaxValue : r.PressTimeMs)
            .Select((r, index) => new BuzzRanking(r.ParticipantId, index + 1, r.PressTimeMs))
            .ToList();
    }
}
