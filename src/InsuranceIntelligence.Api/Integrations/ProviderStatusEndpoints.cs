using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Integrations;

public static class ProviderStatusEndpoints
{
    public static IResult GetProviderStatus(InfrastructureOptions options) =>
        Results.Ok(AdminEndpoints.BuildProviderHealth(options));
}
