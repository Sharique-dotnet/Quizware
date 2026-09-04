namespace QuizApp.Domain.Common.Exceptions;

public sealed class FormatInUseException : Exception
{
    public FormatInUseException(string message)
        : base(message)
    {
    }
}
