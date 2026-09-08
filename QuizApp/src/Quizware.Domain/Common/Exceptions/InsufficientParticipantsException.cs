namespace Quizware.Domain.Common.Exceptions;

public sealed class InsufficientParticipantsException : Exception
{
    public InsufficientParticipantsException(string message)
        : base(message)
    {
    }
}
