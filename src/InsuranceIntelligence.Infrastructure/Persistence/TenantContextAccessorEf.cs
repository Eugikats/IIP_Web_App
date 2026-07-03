using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class TenantContextAccessorEf : ITenantContextAccessor, ITenantContextSetter
{
    private TenantContext? _current;

    public TenantContext Current =>
        _current ?? throw new InvalidOperationException("Tenant context has not been initialized for this request.");

    public void SetCurrent(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new InvalidOperationException("Tenant context must include a tenant id.");
        }

        if (string.IsNullOrWhiteSpace(context.WorkspaceId))
        {
            throw new InvalidOperationException("Tenant context must include a workspace id.");
        }

        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new InvalidOperationException("Tenant context must include a user id.");
        }

        _current = context;
    }
}
