namespace QuizApp.Application.Common.Exceptions;

/// <summary>Carries every failing field, not just the first — mapped to
/// VALIDATION_FAILED (400) by the API's global exception handler.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
