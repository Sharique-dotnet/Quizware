using Quizware.Application.Abstractions;
using Quizware.Application.Authorization;

namespace Quizware.Api.Middleware;

/// <summary>ADR-002: the tenant id comes from the JWT claim, never the
/// route. If a request carries a {programId} route value (or the value an
/// endpoint names with <see cref="ProgramRouteAttribute"/>), it must match the
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
        var programRoute = context.GetEndpoint()?.Metadata.GetMetadata<ProgramRouteAttribute>();
        var parameter = programRoute?.ParameterName ?? "programId";
        if (context.Request.RouteValues.TryGetValue(parameter, out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeProgramId))
        {
            var unscopedSuperAdmin = programRoute?.AllowUnscopedSuperAdmin == true
                && !currentProgram.HasProgram
                && context.User.IsInRole(Roles.SuperAdmin);
            if (!unscopedSuperAdmin && (!currentProgram.HasProgram || currentProgram.ProgramId != routeProgramId))
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
