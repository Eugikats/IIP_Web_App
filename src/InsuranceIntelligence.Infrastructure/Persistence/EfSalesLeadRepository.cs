using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Tenant-scoped persistence for sales leads (JSON payload), one record per lead keyed by a
/// deterministic id from the lead id. Backs both the read side (get/list) and the manual
/// create path. Channel ingestion and integration-fabric import (later gates) write into the
/// same store via <see cref="ISalesLeadWriteRepository"/>.
/// </summary>
public sealed class EfSalesLeadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ISalesLeadReadRepository, ISalesLeadWriteRepository
{
    public async Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
        string tenantId,
        string workspaceId,
        string leadId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var payloads = await Scoped(DbContext.SalesLeads.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return payloads
            .Select(Deserialize<SalesLeadDetailReadModel>)
            .FirstOrDefault(lead => string.Equals(lead.LeadId, leadId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var payloads = await Scoped(DbContext.SalesLeads.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return payloads.Select(Deserialize<SalesLeadDetailReadModel>).ToArray();
    }

    public Task CreateAsync(
        string tenantId,
        string workspaceId,
        SalesLeadDetailReadModel lead,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var id = DeterministicId(tenantId, workspaceId, lead.LeadId);
        return UpsertAsync(DbContext.SalesLeads, id, scope, Serialize(lead), cancellationToken: cancellationToken);
    }

    public async Task<SalesLeadDetailReadModel?> FindByPhoneAsync(
        string tenantId,
        string workspaceId,
        string phone,
        CancellationToken cancellationToken = default)
    {
        var leads = await ListLeadsAsync(tenantId, workspaceId, cancellationToken);
        return leads
            .Where(lead => PhoneNumbers.SameNumber(lead.Phone ?? lead.ContactInfo, phone))
            .OrderByDescending(lead => lead.LastInteractionAt ?? lead.CreatedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private static Guid DeterministicId(string tenantId, string workspaceId, string leadId)
    {
        var composite = $"{tenantId}|{workspaceId}|{leadId.ToLowerInvariant()}";
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(composite)));
    }
}
