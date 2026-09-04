namespace QuizApp.Domain.Common.Exceptions;

public sealed class SegmentNotReorderableException : Exception
{
    public SegmentNotReorderableException(string message)
        : base(message)
    {
    }
}
