using RaiseTracker.Api.Models;

namespace RaiseTracker.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Dictionary<string, List<DateTime>> _loginAttempts = new();
    private static readonly object _lock = new();
    private const int MaxAttempts = 5;
    private const int WindowMinutes = 15;

    public static void ClearAttempts(string key)
    {
        lock (_lock)
        {
            if (_loginAttempts.ContainsKey(key))
            {
                _loginAttempts[key].Clear();
            }
        }
    }

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (path == "/api/login" && context.Request.Method == "POST")
        {
            var key = $"{context.Connection.RemoteIpAddress}";
            var now = DateTime.UtcNow;

            bool shouldBlock = false;
            lock (_lock)
            {
                if (!_loginAttempts.ContainsKey(key))
                {
                    _loginAttempts[key] = new List<DateTime>();
                }

                // Remove old attempts
                _loginAttempts[key].RemoveAll(t => (now - t).TotalMinutes > WindowMinutes);

                if (_loginAttempts[key].Count >= MaxAttempts)
                {
                    shouldBlock = true;
                }
                else
                {
                    // Track this attempt
                    _loginAttempts[key].Add(now);
                }
            }

            if (shouldBlock)
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Error = "Too many login attempts. Please try again later.",
                    Code = "RATE_LIMIT_EXCEEDED"
                });
                return;
            }

            // Store key in context for potential clearing on success
            context.Items["RateLimitKey"] = key;
        }

        await _next(context);
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
