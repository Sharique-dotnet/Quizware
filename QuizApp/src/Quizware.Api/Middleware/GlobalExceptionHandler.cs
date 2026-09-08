using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quizware.Domain.Common.Exceptions;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Api.Middleware;

/// <summary>Maps every domain exception from P1-14 (plus validation and the
/// P1-02 NoActiveParticipantsException) to its documented error code, as an
/// RFC 9457 application/problem+json response.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, title) = Map(exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "{ErrorCode} for {Path}", errorCode, httpContext.Request.Path);
        }

        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].ToString();

        var problemDetails = new ProblemDetails
        {
            Type = $"https://quizapp/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}",
            Title = title,
            Status = statusCode,
            Detail = statusCode >= 500 ? "An unexpected error occurred." : exception.Message,
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        problemDetails.Extensions["correlationId"] = correlationId;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        if (exception is FormatInUseException formatInUseException)
        {
            problemDetails.Extensions["formatCode"] = formatInUseException.FormatCode;
            problemDetails.Extensions["usedBy"] = formatInUseException.UsedBy;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string ErrorCode, string Title) Map(Exception exception) => exception switch
    {
        ValidationException => (400, "VALIDATION_FAILED", "One or more validation errors occurred"),
        InvalidStateTransitionException => (409, "CONFLICT_STATE", "Action not allowed in the current state"),
        InsufficientParticipantsException => (409, "INSUFFICIENT_PARTICIPANTS", "Fewer than the required active participants"),
        NoActiveParticipantsException => (409, "INSUFFICIENT_PARTICIPANTS", "No active participants remain"),
        QuestionPoolExhaustedException => (409, "QUESTION_POOL_EXHAUSTED", "Not enough questions available"),
        ScoringRuleNotFoundException => (409, "SCORING_RULE_MISSING", "No scoring rule for this format and outcome"),
        UnresolvedTieException => (409, "UNRESOLVED_TIE", "A tie affecting a qualifying place is still open"),
        SegmentNotReorderableException => (409, "SEGMENT_NOT_REORDERABLE", "Segment cannot be reordered"),
        FormatInUseException => (409, "FORMAT_IN_USE", "Format is still in use"),
        UnauthorizedAccessException => (403, "FORBIDDEN", "Role or program scope does not allow this"),
        KeyNotFoundException => (404, "NOT_FOUND", "Entity does not exist in this program"),
        _ => (500, "INTERNAL_ERROR", "An unexpected error occurred"),
    };
}
