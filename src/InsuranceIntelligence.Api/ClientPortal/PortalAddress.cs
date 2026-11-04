using InsuranceIntelligence.Application.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.ClientPortal;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

public sealed class PortalAddress(InfrastructureOptions options, IPortalDirectory directory) : IPortalAddress
{
    public string? BaseUrlFor(TenantScope scope)
    {
        if (directory.ForScope(scope) is { HostnameIsVerified: true } identity)
        {
            return $"https://{identity.Hostname}";
        }

        if (!string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl))
        {
            return options.ClientPortalBaseUrl.TrimEnd('/');
        }

        // Development falls back to the local portal; any other profile without an address gets no link rather than a broken one.
        return options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "http://localhost:3000" : null;
    }

    public string LinkQueryFor(TenantScope scope) =>
        directory.ForScope(scope) is { } identity && !identity.HostnameIsVerified ? $"&t={Uri.EscapeDataString(identity.Slug)}" : string.Empty;
}
