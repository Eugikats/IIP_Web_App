using InsuranceIntelligence.Domain.Organisations;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfOrganisationRepository(InsuranceIntelligenceDbContext db) : IOrganisationRepository
{
    public async Task AddAsync(Organisation organisation, CancellationToken cancellationToken = default)
    {
        db.Organisations.Add(new OrganisationEntity
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.OrganisationId,
            Name = organisation.Name,
            Type = organisation.Type.ToString(),
            DefaultWorkspaceId = organisation.DefaultWorkspaceId,
            Status = organisation.Status,
            PlanId = organisation.PlanId,
            CreatedByUserId = organisation.CreatedByUserId,
            CreatedAt = organisation.CreatedAt,
            MarketplaceSubscriptionId = organisation.MarketplaceSubscriptionId,
            Product = organisation.Product.ToString(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOrganisationAsync(Organisation organisation, CancellationToken cancellationToken = default)
    {
        var entity = await db.Organisations.FirstAsync(o => o.OrganisationId == organisation.OrganisationId, cancellationToken);
        entity.Name = organisation.Name;
        entity.Status = organisation.Status;
        entity.PlanId = organisation.PlanId;
        entity.MarketplaceSubscriptionId = organisation.MarketplaceSubscriptionId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Organisation?> GetAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.OrganisationId == organisationId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<Organisation?> GetByMarketplaceSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.MarketplaceSubscriptionId == subscriptionId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Organisation>> GetManyAsync(IEnumerable<string> organisationIds, CancellationToken cancellationToken = default)
    {
        var ids = organisationIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var entities = await db.Organisations.AsNoTracking().Where(o => ids.Contains(o.OrganisationId)).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToArray();
    }

    public Task<int> CountCreatedByAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Organisations.AsNoTracking().CountAsync(o => o.CreatedByUserId == userId, cancellationToken);

    public async Task<IReadOnlyList<OrganisationMembership>> ListMembershipsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var memberships = await db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => new { m.TenantId, m.WorkspaceId })
            .ToListAsync(cancellationToken);
        var roles = await db.RoleAssignments.AsNoTracking().Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        return memberships
            .Select(m => new OrganisationMembership(
                m.TenantId, m.WorkspaceId,
                roles.Where(r => r.TenantId == m.TenantId && r.WorkspaceId == m.WorkspaceId).Select(r => r.RoleCode).Distinct().ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListMemberUserIdsAsync(string organisationId, CancellationToken cancellationToken = default) =>
        await db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.TenantId == organisationId && m.IsActive && !m.UserId.StartsWith(ApiKeyRecord.UserPrefix))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task AddInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken = default)
    {
        db.OrganisationInvitations.Add(ToEntity(invitation));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken = default)
    {
        var entity = await db.OrganisationInvitations.FirstAsync(i => i.Id == invitation.InvitationId, cancellationToken);
        entity.Status = invitation.Status.ToString();
        entity.AcceptedByUserId = invitation.AcceptedByUserId;
        entity.AcceptedAt = invitation.AcceptedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrganisationInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var entity = await db.OrganisationInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<OrganisationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var entity = await db.OrganisationInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<OrganisationInvitation>> ListInvitationsAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var entities = await db.OrganisationInvitations.AsNoTracking().Where(i => i.OrganisationId == organisationId).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToArray();
    }

    public async Task AddLinkAsync(BrokerLink link, CancellationToken cancellationToken = default)
    {
        db.BrokerLinks.Add(ToEntity(link));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLinkAsync(BrokerLink link, CancellationToken cancellationToken = default)
    {
        var entity = await db.BrokerLinks.FirstAsync(l => l.Id == link.LinkId, cancellationToken);
        entity.Status = link.Status.ToString();
        entity.DecidedByUserId = link.DecidedByUserId;
        entity.UpdatedAt = link.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BrokerLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var entity = await db.BrokerLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Id == linkId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<BrokerLink>> ListLinksForOrganisationAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var entities = await db.BrokerLinks.AsNoTracking()
            .Where(l => l.BrokerOrganisationId == organisationId || l.InsurerOrganisationId == organisationId)
            .ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToArray();
    }

    private static Organisation ToDomain(OrganisationEntity e) => new(
        e.OrganisationId, e.Name, Enum.Parse<OrganisationType>(e.Type), e.DefaultWorkspaceId, e.Status, e.PlanId,
        e.CreatedByUserId, e.CreatedAt, e.MarketplaceSubscriptionId,
        Enum.TryParse<OrganisationProduct>(e.Product, out var product) ? product : OrganisationProduct.Insurance);

    private static OrganisationInvitationEntity ToEntity(OrganisationInvitation i) => new()
    {
        Id = i.InvitationId, OrganisationId = i.OrganisationId, Email = i.Email, RoleCode = i.RoleCode, TokenHash = i.TokenHash,
        Status = i.Status.ToString(), InvitedByUserId = i.InvitedByUserId, CreatedAt = i.CreatedAt, ExpiresAt = i.ExpiresAt,
        AcceptedByUserId = i.AcceptedByUserId, AcceptedAt = i.AcceptedAt,
    };

    private static OrganisationInvitation ToDomain(OrganisationInvitationEntity e) => new(
        e.Id, e.OrganisationId, e.Email, e.RoleCode, e.TokenHash, Enum.Parse<InvitationStatus>(e.Status),
        e.InvitedByUserId, e.CreatedAt, e.ExpiresAt, e.AcceptedByUserId, e.AcceptedAt);

    private static BrokerLinkEntity ToEntity(BrokerLink l) => new()
    {
        Id = l.LinkId, BrokerOrganisationId = l.BrokerOrganisationId, InsurerOrganisationId = l.InsurerOrganisationId,
        Status = l.Status.ToString(), InitiatedBy = l.InitiatedBy.ToString(), RequestedByUserId = l.RequestedByUserId,
        DecidedByUserId = l.DecidedByUserId, CreatedAt = l.CreatedAt, UpdatedAt = l.UpdatedAt,
    };

    private static BrokerLink ToDomain(BrokerLinkEntity e) => new(
        e.Id, e.BrokerOrganisationId, e.InsurerOrganisationId, Enum.Parse<BrokerLinkStatus>(e.Status),
        Enum.Parse<LinkInitiator>(e.InitiatedBy), e.RequestedByUserId, e.DecidedByUserId, e.CreatedAt, e.UpdatedAt);
}
