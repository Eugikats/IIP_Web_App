namespace InsuranceIntelligence.Infrastructure.Configuration;

/// <summary>
/// SaaS mode must start clean or not at all. A deployment-wide tenant, workspace or WhatsApp
/// connection left in configuration is exactly the kind of default that sends one insurer's traffic
/// to another, so its presence is a startup error, not a warning.
/// </summary>
public static class MultiTenantStartupGuard
{
    public static IReadOnlyList<string> Problems(InfrastructureOptions options)
    {
        if (!options.MultiTenant)
        {
            return [];
        }

        var problems = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.TenantId))
        {
            problems.Add("INSURANCEINTELLIGENCE_TENANT_ID must not be set when INSURANCEINTELLIGENCE_MULTI_TENANT is true.");
        }

        if (!string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            problems.Add("INSURANCEINTELLIGENCE_WORKSPACE_ID must not be set when INSURANCEINTELLIGENCE_MULTI_TENANT is true.");
        }

        if (!string.IsNullOrWhiteSpace(options.WhatsApp.PhoneNumberId) || !string.IsNullOrWhiteSpace(options.WhatsApp.AccessToken))
        {
            problems.Add("A deployment-wide WhatsApp connection (WhatsApp:PhoneNumberId / WhatsApp:AccessToken) must not be set when INSURANCEINTELLIGENCE_MULTI_TENANT is true: each insurer connects its own number.");
        }

        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !(options.RuntimeProfile ?? string.Empty).Equals("LocalTest", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Multi-tenant mode requires real sign-in; Development auth is only allowed under the LocalTest profile.");
        }

        return problems;
    }
}
