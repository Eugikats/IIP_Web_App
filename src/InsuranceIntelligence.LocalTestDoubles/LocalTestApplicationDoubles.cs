using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class LocalTestPermissionReadRepository : IPermissionReadRepository
{
    public Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        string tenantId,
        string workspaceId,
        string userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Permission>>(Array.Empty<Permission>());

    public Task<EffectiveMembership?> GetEffectiveMembershipAsync(
        string userId,
        string? tenantHint,
        string? workspaceHint,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<EffectiveMembership?>(null);
}

public sealed class LocalTestAuditEventRepository : IAuditEventRepository
{
    private readonly List<AuditEvent> _events = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<AuditEvent>>(_events.Where(item => item.Scope.Matches(scope)).ToArray());

    public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(
        Guid caseId,
        TenantScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<AuditEvent>>(
            _events.Where(item => item.CaseId == caseId && item.Scope.Matches(scope)).ToArray());
}

public sealed class LocalTestClaimsReadRepository(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    ITenantContextAccessor tenantContextAccessor) : IClaimsReadRepository
{
    public async Task<IReadOnlyCollection<ClaimCase>> ListAsync(
        ClaimsQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        var claims = (await caseRepository.ListAsync(scope, cancellationToken)).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            claims = claims.Where(claim =>
                claim.CaseId.ToString().Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                || claim.CustomerName.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                || claim.PolicyNumber.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        }

        if (query.AttentionOnly == true)
        {
            claims = claims.Where(claim =>
                claim.Status is not CaseStatus.Closed and not CaseStatus.Settled
                && (claim.CompletenessScore < 100m || claim.SlaDueAt <= DateTimeOffset.UtcNow.AddHours(4)));
        }

        return claims
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 200))
            .ToArray();
    }

    public async Task<ClaimCase?> GetAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(claimId, cancellationToken);
        return claim is not null && claim.Scope.Matches(tenantContextAccessor.Current.Scope) ? claim : null;
    }

    public Task<IReadOnlyCollection<CaseDocument>> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        documentRepository.ListForCaseAsync(claimId, tenantContextAccessor.Current.Scope, cancellationToken);
}

public sealed class LocalTestProviderHealthCheck : IProviderHealthCheck
{
    public Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProviderStatus(providerCode, "not_configured", "LocalTest provider health does not simulate live Azure connectivity.", DateTimeOffset.UtcNow));
}
