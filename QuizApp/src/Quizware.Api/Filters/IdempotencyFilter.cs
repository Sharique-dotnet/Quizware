using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;
using Quizware.Application.Abstractions;
using Quizware.Infrastructure.Idempotency;

namespace Quizware.Api.Filters;

/// <summary>Same Idempotency-Key + same body replays the stored response.
/// Same key + a different body is a 409 IDEMPOTENCY_MISMATCH. No header
/// present means the request is not deduplicated at all.</summary>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";

    private readonly IIdempotencyStore _store;
    private readonly ICurrentProgram _currentProgram;

    public IdempotencyFilter(IIdempotencyStore store, ICurrentProgram currentProgram)
    {
        _store = store;
        _currentProgram = currentProgram;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) || !_currentProgram.HasProgram)
        {
            await next();
            return;
        }

        var key = keyValues.ToString();
        var programId = _currentProgram.ProgramId;

        context.HttpContext.Request.EnableBuffering();
        using var reader = new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.HttpContext.Request.Body.Position = 0;

        var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

        var existing = await _store.FindAsync(programId, key, context.HttpContext.RequestAborted);
        if (existing is not null)
        {
            if (existing.RequestBodyHash != bodyHash)
            {
                context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
                {
                    type = "https://quizapp/errors/idempotency-mismatch",
                    title = "Same idempotency key, different request body",
                    status = 409,
                    errorCode = "IDEMPOTENCY_MISMATCH",
                })
                { StatusCode = 409 };
                return;
            }

            context.Result = new Microsoft.AspNetCore.Mvc.ContentResult
            {
                StatusCode = existing.ResponseStatusCode,
                Content = existing.ResponseBody,
                ContentType = "application/json",
            };
            return;
        }

        var executed = await next();

        if (executed.Result is Microsoft.AspNetCore.Mvc.ObjectResult { StatusCode: >= 200 and < 300 } objectResult)
        {
            var responseBody = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
            await _store.SaveAsync(programId, key, bodyHash, objectResult.StatusCode ?? 200, responseBody, context.HttpContext.RequestAborted);
        }
    }
}
