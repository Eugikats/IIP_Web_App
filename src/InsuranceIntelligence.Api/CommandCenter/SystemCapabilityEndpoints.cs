using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.CommandCenter;

public static class SystemCapabilityEndpoints
{
    public static async Task<IResult> GetCapabilities(
        ISystemCapabilityStatusService statusService,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var status = await statusService.GetAsync(tenantContext.TenantId, cancellationToken);
        return Results.Ok(status);
    }
}
