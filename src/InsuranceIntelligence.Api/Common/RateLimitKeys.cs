using System.Threading.RateLimiting;
using InsuranceIntelligence.Api.Auth;

namespace InsuranceIntelligence.Api.Common;

/// <summary>
/// Limits on the doors anyone can knock on: sign-up, accepting an invitation, the Marketplace
/// landing page and its webhook. The key is the signed-in person when there is one, otherwise the
/// address the request came from as the platform front door reports it (the web server sits
/// behind a proxy, so the socket address is the proxy, which would put everyone in one bucket).
/// </summary>
public static class RateLimitKeys
{
    public static string For(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true
            && DemoAuthorization.GetUserId(httpContext, fallback: string.Empty) is { Length: > 0 } userId)
        {
            return $"user:{userId}";
        }

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // App Service writes ip:port for IPv4.
            var colon = forwarded.LastIndexOf(':');
            return $"ip:{(forwarded.Count(c => c == ':') == 1 && colon > 0 ? forwarded[..colon] : forwarded)}";
        }

        return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    public static RateLimitPartition<string> FixedWindow(HttpContext httpContext, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(For(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
        });
}
