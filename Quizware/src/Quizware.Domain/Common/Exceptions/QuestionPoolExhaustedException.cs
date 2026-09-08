namespace Quizware.Domain.Common.Exceptions;

public sealed class QuestionPoolExhaustedException : Exception
{
    public QuestionPoolExhaustedException(string message)
        : base(message)
    {
    }
}
