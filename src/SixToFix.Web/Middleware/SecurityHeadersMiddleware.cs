namespace SixToFix.Web.Middleware;

/// <summary>
/// Adds security headers including a Content Security Policy (CSP) on every response.
/// CSP directives are tuned for Blazor Server + Google Fonts (Inter + Playfair Display).
///
/// Key decisions:
///   style-src   — 'unsafe-inline' required by Blazor's scoped-CSS isolation mechanism.
///   script-src  — 'unsafe-inline' required by Blazor Server's inline bootstrapping script.
///   connect-src — includes wss: for SignalR WebSocket and Google Fonts CDN hosts.
///   font-src    — gstatic.com (font file CDN) + data: for any base64-inlined fonts.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["Content-Security-Policy"] = Csp;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        await _next(context);
    }
}
