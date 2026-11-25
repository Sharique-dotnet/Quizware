using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Quizware.Api.Security;

/// <summary>P15-02 (OWASP A07): sign-in and token refresh are limited per
/// client address (<c>Security:AuthRequestsPerMinute</c>, default 10), on top
/// of the per-account lockout, so a password cannot be guessed at speed.
/// Over the limit is a 429 <c>RATE_LIMITED</c>.</summary>
public static class AuthRateLimiting
{
    public const string PolicyName = "auth";
    public const string ConfigurationKey = "Security:AuthRequestsPerMinute";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue(ConfigurationKey, 10);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://quizapp/errors/rate-limited",
                        title = "Too many requests; try again in a minute",
                        status = 429,
                        errorCode = "RATE_LIMITED",
                    },
                    cancellationToken);
            };
        });
    }
}
