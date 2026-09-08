using Quizware.Application.Abstractions;

namespace Quizware.Api.Middleware;

/// <summary>ADR-002: the tenant id comes from the JWT claim, never the
/// route. If a request carries a {programId} route value, it must match the
/// token's program_id claim exactly, or the request is rejected with 403
/// before any database access — never silently redirected to the token's
/// program.</summary>
public sealed class ProgramScopeMiddleware
{
    private readonly RequestDelegate _next;

    public ProgramScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentProgram currentProgram)
    {
        if (context.Request.RouteValues.TryGetValue("programId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeProgramId))
        {
            if (!currentProgram.HasProgram || currentProgram.ProgramId != routeProgramId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://quizapp/errors/forbidden",
                    title = "Role or program scope does not allow this",
                    status = 403,
                    errorCode = "FORBIDDEN",
                    instance = context.Request.Path.Value,
                });
                return;
            }
        }

        await _next(context);
    }
}
