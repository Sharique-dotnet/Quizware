namespace Quizware.Domain.Common.Exceptions;

public sealed class ScoringRuleNotFoundException : Exception
{
    public ScoringRuleNotFoundException(string message)
        : base(message)
    {
    }
}
