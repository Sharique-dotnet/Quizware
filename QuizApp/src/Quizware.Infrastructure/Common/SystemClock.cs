using Quizware.Application.Abstractions;

namespace Quizware.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
