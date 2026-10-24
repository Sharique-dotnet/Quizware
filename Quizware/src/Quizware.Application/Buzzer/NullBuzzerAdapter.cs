using Quizware.Domain.Common.Exceptions;

namespace Quizware.Application.Buzzer;

/// <summary>The default provider: no hardware. <c>IsAvailable = false</c>, so
/// the operator console shows manual buttons and answers are recorded with
/// <c>answerSource = Operator</c>. It lives in Application rather than in the
/// buzzer module so that something still answers when the module is deleted.</summary>
public sealed class NullBuzzerAdapter : IBuzzerProvider
{
    public const string ProviderName = "None";

    private readonly string _reason;

    public NullBuzzerAdapter()
        : this(0, "The buzzer module is not installed.")
    {
    }

    public NullBuzzerAdapter(int deviceCount, string reason)
    {
        DeviceCount = deviceCount;
        _reason = reason;
    }

    public string Name => ProviderName;

    public bool IsAvailable => false;

    public int DeviceCount { get; }

    public Task<BuzzerHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BuzzerHealth(false, _reason, []));

    public Task ArmAsync(BuzzArmRequest request, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<BuzzPressResult>> CollectAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task ResetAsync(Guid sessionId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<BuzzDeviceReading>> TestAsync(IReadOnlyList<int> deviceIds, CancellationToken cancellationToken) =>
        throw Unavailable();

    private BuzzerUnavailableException Unavailable() => new(_reason);
}
