using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Core;

public sealed record TenantContext(
    string TenantId,
    string WorkspaceId,
    string UserId,
    IReadOnlyCollection<string> RoleCodes)
{
    public TenantScope Scope => TenantScope.Create(TenantId, WorkspaceId);
}

public interface ITenantContextAccessor
{
    TenantContext Current { get; }
}

public interface ITenantContextSetter
{
    void SetCurrent(TenantContext context);
}

public sealed class TenantContextAccessor : ITenantContextAccessor, ITenantContextSetter
{
    private TenantContext? _current;

    public TenantContext Current =>
        _current ?? throw new InvalidOperationException("Tenant context has not been initialized for this request.");

    public void SetCurrent(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _current = context;
    }
}
