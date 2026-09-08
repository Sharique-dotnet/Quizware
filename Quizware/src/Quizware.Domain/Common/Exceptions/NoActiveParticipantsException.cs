namespace Quizware.Domain.Common.Exceptions;

public sealed class NoActiveParticipantsException : Exception
{
    public NoActiveParticipantsException()
        : base("No active participants remain to take a turn.")
    {
    }
}
