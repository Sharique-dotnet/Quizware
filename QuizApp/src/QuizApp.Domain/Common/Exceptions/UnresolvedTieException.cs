namespace QuizApp.Domain.Common.Exceptions;

public sealed class UnresolvedTieException : Exception
{
    public UnresolvedTieException(string message)
        : base(message)
    {
    }
}
