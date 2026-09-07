using QuizApp.Application.Abstractions;

namespace QuizApp.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
