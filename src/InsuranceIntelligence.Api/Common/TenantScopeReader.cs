using System.Security.Claims;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Common;

public static class TenantScopeReader
{
    public static bool TryGetFromHeaders(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        if (TryGetFromClaims(httpContext.User, out scope, out validationError))
        {
            return true;
        }

        var options = ResolveOptions(httpContext);
        if (IsProtectedAuthMode(options))
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return TryGetFromConfiguredScope(options!, out scope, out validationError);
        }

        var tenantId = ReadHeader(httpContext, "X-Tenant-Id");
        var workspaceId = ReadHeader(httpContext, "X-Workspace-Id");

        return TryCreate(tenantId, workspaceId, out scope, out validationError);
    }

    public static bool TryGetFromHeadersOrQuery(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        if (TryGetFromClaims(httpContext.User, out scope, out validationError))
        {
            return true;
        }

        var options = ResolveOptions(httpContext);
        if (IsProtectedAuthMode(options))
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return TryGetFromConfiguredScope(options!, out scope, out validationError);
        }

        var tenantId = ReadHeader(httpContext, "X-Tenant-Id")
            ?? ReadQuery(httpContext, "tenantId");
        var workspaceId = ReadHeader(httpContext, "X-Workspace-Id")
            ?? ReadQuery(httpContext, "workspaceId");

        return TryCreate(tenantId, workspaceId, out scope, out validationError);
    }

    public static bool TryGetFromConfiguredScope(
        InfrastructureOptions options,
        out TenantScope scope,
        out string validationError)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId)
            && string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            scope = new TenantScope(string.Empty, string.Empty);
            validationError = "Authenticated identity is missing tenant/workspace claims and no deployment tenant/workspace scope is configured.";
            return false;
        }

        return TryCreate(options.TenantId, options.WorkspaceId, out scope, out validationError);
    }

    public static bool TryGetFromClaims(ClaimsPrincipal user, out TenantScope scope, out string validationError)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            scope = new TenantScope(string.Empty, string.Empty);
            validationError = "Authenticated tenant and workspace claims are required.";
            return false;
        }

        var tenantId = FirstClaimValue(user, RuntimeClaimTypes.TenantScope);
        var workspaceId = FirstClaimValue(user, RuntimeClaimTypes.WorkspaceScope);

        return TryCreate(tenantId, workspaceId, out scope, out validationError);
    }

    private static bool TryCreate(string? tenantId, string? workspaceId, out TenantScope scope, out string validationError)
    {
        try
        {
            scope = TenantScope.Create(tenantId ?? string.Empty, workspaceId ?? string.Empty);
            validationError = string.Empty;
            return true;
        }
        catch (ArgumentException ex)
        {
            scope = new TenantScope(string.Empty, string.Empty);
            validationError = ex.Message;
            return false;
        }
    }

    private static string? ReadHeader(HttpContext httpContext, string headerName)
    {
        if (httpContext.Request.Headers.TryGetValue(headerName, out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return null;
    }

    private static string? ReadQuery(HttpContext httpContext, string queryName)
    {
        if (httpContext.Request.Query.TryGetValue(queryName, out var query)
            && !string.IsNullOrWhiteSpace(query))
        {
            return query.ToString();
        }

        return null;
    }

    private static InfrastructureOptions? ResolveOptions(HttpContext httpContext)
    {
        try
        {
            var services = httpContext.RequestServices;
            return services?.GetService<InfrastructureOptions>();
        }
        catch (ArgumentNullException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsProtectedAuthMode(InfrastructureOptions? options) =>
        options is not null
        && !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);

    private static string? FirstClaimValue(ClaimsPrincipal user, IReadOnlyCollection<string> claimTypes) =>
        claimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
