using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <param name="checkOrganisationStatus">Multi-tenant mode only. A single-tenant deployment has no organisations
/// and may not yet have their table, so it never looks.</param>
public sealed class EfPermissionReadRepository(InsuranceIntelligenceDbContext dbContext, bool checkOrganisationStatus = true) : IPermissionReadRepository
{
    private EffectivePermissionCache? _effectivePermissionCache;

    public async Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        string tenantId,
        string workspaceId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_effectivePermissionCache is { } cached
            && cached.TenantId == tenantId
            && cached.WorkspaceId == workspaceId
            && cached.UserId == userId)
        {
            return cached.Permissions;
        }

        var hasMembership = await dbContext.UserTenantMemberships.AsNoTracking()
            .AnyAsync(item =>
                item.TenantId == tenantId
                && item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.IsActive,
                cancellationToken);

        if (!hasMembership)
        {
            return Array.Empty<Permission>();
        }

        var roleCodes = dbContext.RoleAssignments.AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.WorkspaceId == workspaceId
                && item.UserId == userId)
            .Select(item => item.RoleCode);

        var permissionCodes = await dbContext.RolePermissions.AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.WorkspaceId == workspaceId
                && roleCodes.Contains(item.RoleCode))
            .Select(item => item.PermissionCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return permissionCodes
            .Select(code => Enum.TryParse<Permission>(code, ignoreCase: true, out var permission) ? permission : (Permission?)null)
            .OfType<Permission>()
            .ToArray();
    }

    public async Task<EffectiveMembership?> GetEffectiveMembershipAsync(
        string userId,
        string? tenantHint,
        string? workspaceHint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        // Candidate active memberships for this authenticated user, narrowed by the hints when present.
        var query = dbContext.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            query = query.Where(m => m.TenantId == tenantHint);
        }
        if (!string.IsNullOrWhiteSpace(workspaceHint))
        {
            query = query.Where(m => m.WorkspaceId == workspaceHint);
        }

        var memberships = await query
            .Select(m => new { m.TenantId, m.WorkspaceId })
            .ToArrayAsync(cancellationToken);

        // No direct membership in the organisation asked for: a broker whose organisation has an
        // active link with that insurer reaches it, as a broker, for exactly as long as the link stands.
        if (memberships.Length == 0 && !string.IsNullOrWhiteSpace(tenantHint))
        {
            return await BrokerLinkedMembershipAsync(userId, tenantHint, workspaceHint, cancellationToken);
        }

        // Fail closed: no membership, or ambiguous (multiple) without a hint to disambiguate.
        if (memberships.Length != 1)
        {
            return null;
        }

        var scope = memberships[0];

        // A suspended, cancelled or closed organisation is a tenant nobody can enter, whatever
        // memberships remain. Deployments with no organisations (Managed Application) have no row
        // here and are unaffected.
        var organisation = checkOrganisationStatus
            ? await dbContext.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.OrganisationId == scope.TenantId, cancellationToken)
            : null;
        if (organisation is not null && organisation.Status != "Active")
        {
            return null;
        }

        var roles = await dbContext.RoleAssignments.AsNoTracking()
            .Where(r => r.TenantId == scope.TenantId && r.WorkspaceId == scope.WorkspaceId && r.UserId == userId)
            .Select(r => r.RoleCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        // Effective permissions = the code-defined role→permission map for the assigned roles
        // (IIP's canonical source, as PermissionService also uses) UNION any per-tenant overrides
        // in the RolePermissions table. This means an assigned role (e.g. Admin) is authoritative
        // without every tenant having to re-seed the standard permission set.
        var mapPermissions = roles
            .Select(code => Enum.TryParse<PlatformRole>(code, ignoreCase: true, out var role) ? role : (PlatformRole?)null)
            .OfType<PlatformRole>()
            .SelectMany(RolePermissionMap.ForRole);
        var overridePermissionCodes = await dbContext.RolePermissions.AsNoTracking()
            .Where(item =>
                item.TenantId == scope.TenantId
                && item.WorkspaceId == scope.WorkspaceId
                && roles.Contains(item.RoleCode))
            .Select(item => item.PermissionCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var overridePermissions = overridePermissionCodes
            .Select(code => Enum.TryParse<Permission>(code, ignoreCase: true, out var permission) ? permission : (Permission?)null)
            .OfType<Permission>();

        var permissions = mapPermissions.Concat(overridePermissions).Distinct().ToArray();
        _effectivePermissionCache = new EffectivePermissionCache(
            scope.TenantId,
            scope.WorkspaceId,
            userId,
            permissions);

        return new EffectiveMembership(scope.TenantId, scope.WorkspaceId, userId, roles, permissions);
    }

    private async Task<EffectiveMembership?> BrokerLinkedMembershipAsync(
        string userId, string insurerOrganisationId, string? workspaceHint, CancellationToken cancellationToken)
    {
        var memberOf = await dbContext.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (memberOf.Count == 0)
        {
            return null;
        }

        var insurer = await dbContext.Organisations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrganisationId == insurerOrganisationId && o.Type == "Insurer" && o.Status == "Active", cancellationToken);
        if (insurer is null || (!string.IsNullOrWhiteSpace(workspaceHint) && workspaceHint != insurer.DefaultWorkspaceId))
        {
            return null;
        }

        var activeBrokers = dbContext.Organisations.AsNoTracking()
            .Where(o => memberOf.Contains(o.OrganisationId) && o.Type == "Broker" && o.Status == "Active")
            .Select(o => o.OrganisationId);
        var linked = await dbContext.BrokerLinks.AsNoTracking()
            .AnyAsync(l => l.InsurerOrganisationId == insurerOrganisationId && l.Status == "Active" && activeBrokers.Contains(l.BrokerOrganisationId), cancellationToken);
        if (!linked)
        {
            return null;
        }

        var permissions = RolePermissionMap.ForRole(PlatformRole.Broker).Distinct().ToArray();
        _effectivePermissionCache = new EffectivePermissionCache(insurer.OrganisationId, insurer.DefaultWorkspaceId, userId, permissions);
        return new EffectiveMembership(insurer.OrganisationId, insurer.DefaultWorkspaceId, userId, [nameof(PlatformRole.Broker)], permissions);
    }

    private sealed record EffectivePermissionCache(
        string TenantId,
        string WorkspaceId,
        string UserId,
        IReadOnlyCollection<Permission> Permissions);
}

public sealed class EfAuditEventRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAuditEventRepository
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AuditEvents,
            auditEvent.EventId,
            auditEvent.Scope,
            Serialize(auditEvent),
            record => record.CaseId = auditEvent.CaseId,
            cancellationToken);

    public async Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
            .Select(record => Deserialize<AuditEvent>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(
        Guid caseId,
        TenantScope scope,
        CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<AuditEvent>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfClaimsReadRepository(
    InsuranceIntelligenceDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor)
    : JsonRepositoryBase(dbContext), IClaimsReadRepository
{
    public async Task<IReadOnlyCollection<ClaimCase>> ListAsync(
        ClaimsQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        var claims = await Scoped(DbContext.Cases.AsNoTracking(), scope)
            .Select(record => Deserialize<ClaimCase>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

        var filtered = claims.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(claim =>
                Contains(claim.CaseId.ToString(), query.Search)
                || Contains(claim.CustomerName, query.Search)
                || Contains(claim.PolicyNumber, query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(claim => claim.Status.ToString().Equals(query.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.RiskLevel))
        {
            filtered = filtered.Where(claim => claim.RiskLevel.Equals(query.RiskLevel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            filtered = filtered.Where(claim => claim.Channel.Equals(query.Channel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedUserId))
        {
            filtered = filtered.Where(claim => string.Equals(claim.AssignedUserId, query.AssignedUserId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.AttentionOnly == true)
        {
            filtered = filtered.Where(NeedsAttention);
        }

        return filtered
            .OrderByDescending(AttentionRank)
            .ThenByDescending(claim => claim.UpdatedAt)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 200))
            .ToArray();
    }

    public async Task<ClaimCase?> GetAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        var record = await Scoped(DbContext.Cases.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == claimId, cancellationToken);

        return record is null ? null : Deserialize<ClaimCase>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<CaseDocument>> ListDocumentsAsync(
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        return await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
            .Where(record => record.CaseId == claimId)
            .Select(record => Deserialize<CaseDocument>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
    }

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool NeedsAttention(ClaimCase claim) =>
        claim.Status is not CaseStatus.Closed and not CaseStatus.Settled
        && (claim.CompletenessScore < 100m
            || claim.Status is CaseStatus.MissingDocuments or CaseStatus.Escalated
            || claim.Priority >= CasePriority.High
            || claim.SlaDueAt <= DateTimeOffset.UtcNow.AddHours(4));

    private static int AttentionRank(ClaimCase claim) =>
        claim.SlaDueAt <= DateTimeOffset.UtcNow ? 0 :
        claim.Priority == CasePriority.Urgent ? 1 :
        claim.Priority == CasePriority.High ? 2 :
        claim.CompletenessScore < 100m ? 3 : 4;
}
