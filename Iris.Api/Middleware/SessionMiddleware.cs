using System.Security.Claims;
using Iris.Models;
using Iris.Services;

namespace Iris.Middleware;

public class SessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuthService _authService;
    private readonly Dictionary<string, Session> _activeSessions = new();
    private const string SessionCookieName = "AuthSession";
    private const int SlidingExpiryThresholdDays = 2;

    public SessionMiddleware(RequestDelegate next, IAuthService authService)
    {
        _next = next;
        _authService = authService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for login endpoints, magic link endpoints, test endpoints, health/heartbeat endpoints, and static files
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/api/users") && context.Request.Method == "GET" ||
            path.StartsWith("/api/login") ||
            path.StartsWith("/api/request-magic-link") ||
            path.StartsWith("/api/validate-magic-link") ||
            path.StartsWith("/api/test-email") ||
            path == "/health" ||
            path == "/api/heartbeat" ||
            path == "/" || path.StartsWith("/raise-tracker.html") || path.StartsWith("/css/") || path.StartsWith("/js/"))
        {
            await _next(context);
            return;
        }

        var cookie = context.Request.Cookies[SessionCookieName];
        Session? session = null;

        if (!string.IsNullOrEmpty(cookie))
        {
            session = _authService.ValidateSessionToken(cookie);

            if (session != null)
            {
                // Check if we have it in memory (for sliding expiry tracking)
                if (_activeSessions.TryGetValue(session.UserId, out var cachedSession))
                {
                    session = cachedSession;
                }
                else
                {
                    _activeSessions[session.UserId] = session;
                }

                // Apply sliding expiry
                var daysUntilExpiry = (session.ExpiresAt - DateTime.UtcNow).TotalDays;
                if (daysUntilExpiry < SlidingExpiryThresholdDays)
                {
                    session.ExpiresAt = DateTime.UtcNow.AddDays(7);
                    _activeSessions[session.UserId] = session;

                    var newToken = _authService.CreateSessionToken(session);
                    context.Response.Cookies.Append(SessionCookieName, newToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = session.ExpiresAt
                    });
                }

                // Attach user info to context
                context.Items["UserId"] = session.UserId;
                context.Items["DisplayName"] = session.DisplayName;
                context.Items["IsAdmin"] = session.IsAdmin;
            }
        }

        if (session == null && path.StartsWith("/api/"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }
}

public static class SessionMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionMiddleware>();
    }
}
