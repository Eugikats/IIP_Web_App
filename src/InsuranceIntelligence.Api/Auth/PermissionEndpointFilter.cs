using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

/// <summary>
/// Enforces a required <see cref="Permission"/> on a route before the handler runs.
///
/// This is applied to consequential write endpoints (approvals, knowledge approval,
/// document upload, etc.) that previously enforced only tenant/workspace scope. It is
/// additive: handler signatures are unchanged. Authorization decisions are delegated to
/// <see cref="DemoAuthorization"/>, which rejects demo-header roles entirely outside
/// Development auth mode, so production builds cannot use browser-selected roles.
/// </summary>
public sealed class PermissionEndpointFilter : IEndpointFilter
{
    private readonly Permission[] _permissions;

    public PermissionEndpointFilter(params Permission[] permissions)
    {
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission must be required.", nameof(permissions));
        }

        _permissions = permissions;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetService<InfrastructureOptions>();
        if (options is null)
        {
            // Fail closed if configuration is unavailable rather than silently allowing access.
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.RequireAny(context.HttpContext, options, _permissions) is { } forbidden)
        {
            return forbidden;
        }

        return await next(context);
    }
}
