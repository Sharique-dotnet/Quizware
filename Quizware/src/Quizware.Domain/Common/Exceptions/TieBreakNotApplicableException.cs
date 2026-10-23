namespace Quizware.Domain.Common.Exceptions;

/// <summary>The tie was already settled, does not affect qualification, or
/// cannot be played any further — 409 <c>TIE_BREAK_NOT_APPLICABLE</c>.</summary>
public sealed class TieBreakNotApplicableException : Exception
{
    public TieBreakNotApplicableException(string message)
        : base(message)
    {
    }
}
