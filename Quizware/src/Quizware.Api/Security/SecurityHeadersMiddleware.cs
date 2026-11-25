namespace Quizware.Api.Security;

/// <summary>P15-02 (OWASP A05): response headers that stop a browser from
/// sniffing content types, framing the API or leaking the URL as a referrer.
/// The API serves JSON only, so its content policy allows nothing; the
/// developer documentation pages (Development only) are left without one.</summary>
public sealed class SecurityHeadersMiddleware
{
    private static readonly string[] DocumentationPaths = ["/swagger", "/scalar", "/openapi"];

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        if (!DocumentationPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        }

        return _next(context);
    }
}
