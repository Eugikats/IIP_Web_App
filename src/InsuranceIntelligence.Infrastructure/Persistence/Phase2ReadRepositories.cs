using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using DomainAuditEvent = InsuranceIntelligence.Domain.Audit.AuditEvent;
using CustomerAuditRow = InsuranceIntelligence.Application.Customers.AuditEventRowDto;

namespace InsuranceIntelligence.Infrastructure.Persistence;

// Read-side adapters that back the Phase 2 query services. Follows the same
// "materialize tenant-scoped rows, then deserialize the JSON payload in memory"
// approach used by EfClaimsReadRepository — the persisted aggregates are stored as
// scoped JSON, so filtering happens after deserialization.

/// <summary>
/// Real audit read model over the AuditEvents table.
/// </summary>
public sealed class EfAuditReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAuditReadRepository
{
    public async Task<IReadOnlyList<AuditEventReadModel>> ListEventsAsync(
        string tenantId,
        string workspaceId,
        AuditEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var scope = TenantScope.Create(tenantId, workspaceId);

        var payloads = await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        var events = payloads
            .Select(Deserialize<DomainAuditEvent>)
            .Select(ToReadModel)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            events = events.Where(item => string.Equals(item.EventType, query.EventType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            events = events.Where(item => string.Equals(item.ActorUserId, query.ActorUserId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            events = events.Where(item => string.Equals(item.TargetType, query.TargetType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TargetId))
        {
            events = events.Where(item => string.Equals(item.TargetId, query.TargetId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromUtc is not null)
        {
            events = events.Where(item => item.OccurredAt >= query.FromUtc.Value);
        }

        if (query.ToUtc is not null)
        {
            events = events.Where(item => item.OccurredAt <= query.ToUtc.Value);
        }

        return events
            .OrderByDescending(item => item.OccurredAt)
            .Skip((Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize))
            .Take(Math.Max(1, query.PageSize))
            .ToArray();
    }

    private static AuditEventReadModel ToReadModel(DomainAuditEvent auditEvent)
    {
        var (targetType, targetId) = ResolveTarget(auditEvent);
        return new AuditEventReadModel(
            auditEvent.EventId.ToString(),
            auditEvent.ActionName,
            auditEvent.UserId,
            targetType,
            targetId,
            auditEvent.AuditReason ?? auditEvent.ActionName,
            auditEvent.Timestamp,
            auditEvent.CorrelationId);
    }

    private static (string? TargetType, string? TargetId) ResolveTarget(DomainAuditEvent auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.DocumentId))
        {
            return ("document", auditEvent.DocumentId);
        }

        return auditEvent.CaseId is { } caseId
            ? ("case", caseId.ToString())
            : (null, null);
    }
}

/// <summary>
/// Real Customer 360 aggregation across the customer's identity, policies, claims,
/// communications, documents, and audit trail. Sources that do not yet exist in the
/// data model (consent, payments, sales opportunities) return empty rather than
/// simulated data.
/// </summary>
public sealed class EfCustomerReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerReadRepository
{
    public async Task<CustomerProfileReadModel?> GetCustomerProfileAsync(
        string tenantId,
        string workspaceId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);

        var customer = (await Scoped(DbContext.Customers.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Customer>)
            .FirstOrDefault(item => string.Equals(item.CustomerId, customerId, StringComparison.OrdinalIgnoreCase));

        if (customer is null)
        {
            return null;
        }

        var policies = (await Scoped(DbContext.Policies.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Policy>)
            .Where(policy => string.Equals(policy.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var claims = (await Scoped(DbContext.Cases.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<ClaimCase>)
            .Where(claim => MatchesCustomer(claim, customer))
            .ToArray();

        var caseIds = claims.Select(claim => claim.CaseId).ToHashSet();

        var interactions = (await Scoped(DbContext.Communications.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Communication>)
            .Where(message =>
                string.Equals(message.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)
                || caseIds.Contains(message.CaseId))
            .OrderByDescending(message => message.CreatedAt)
            .ToArray();

        var documents = caseIds.Count == 0
            ? Array.Empty<CaseDocument>()
            : (await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
                    .Where(record => caseIds.Contains(record.CaseId))
                    .Select(record => record.PayloadJson)
                    .ToArrayAsync(cancellationToken))
                .Select(Deserialize<CaseDocument>)
                .OrderByDescending(document => document.UploadedAt)
                .ToArray();

        var auditHistory = caseIds.Count == 0
            ? Array.Empty<CustomerAuditRow>()
            : (await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
                    .Where(record => record.CaseId != null && caseIds.Contains(record.CaseId.Value))
                    .Select(record => record.PayloadJson)
                    .ToArrayAsync(cancellationToken))
                .Select(Deserialize<DomainAuditEvent>)
                .OrderByDescending(auditEvent => auditEvent.Timestamp)
                .Select(ToCustomerAuditRow)
                .ToArray();

        return new CustomerProfileReadModel(
            customer.CustomerId,
            BuildIdentity(customer),
            policies.Select(BuildPolicy).ToArray(),
            claims.Select(BuildClaim).ToArray(),
            interactions.Select(BuildInteraction).ToArray(),
            documents.Select(BuildDocument).ToArray(),
            new Dictionary<string, string>(),
            Array.Empty<IReadOnlyDictionary<string, string>>(),
            Array.Empty<IReadOnlyDictionary<string, string>>(),
            auditHistory);
    }

    public async Task<IReadOnlyList<CustomerListItemDto>> ListCustomersAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);

        var customers = (await Scoped(DbContext.Customers.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Customer>)
            .ToArray();

        if (customers.Length == 0)
        {
            return Array.Empty<CustomerListItemDto>();
        }

        var claims = (await Scoped(DbContext.Cases.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<ClaimCase>)
            .ToArray();

        return customers
            .OrderBy(customer => customer.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(customer => new CustomerListItemDto(
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone,
                claims.Count(claim => MatchesCustomer(claim, customer) && IsOpenClaim(claim))))
            .ToArray();
    }

    private static bool IsOpenClaim(ClaimCase claim) =>
        claim.Status is not (CaseStatus.Settled or CaseStatus.Closed);

    // ClaimCase stores denormalized customer contact fields (no CustomerId FK). Correlate on
    // email/phone when the customer has them; only fall back to name when no contact info exists,
    // so one customer's claims never leak onto another with the same name.
    private static bool MatchesCustomer(ClaimCase claim, Customer customer)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(customer.Email);
        var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);

        if (hasEmail
            && !string.IsNullOrWhiteSpace(claim.CustomerEmail)
            && string.Equals(customer.Email, claim.CustomerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (hasPhone
            && !string.IsNullOrWhiteSpace(claim.CustomerPhone)
            && string.Equals(NormalizePhone(customer.Phone!), NormalizePhone(claim.CustomerPhone!), StringComparison.Ordinal))
        {
            return true;
        }

        return !hasEmail
            && !hasPhone
            && string.Equals(customer.FullName, claim.CustomerName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static IReadOnlyDictionary<string, string> BuildIdentity(Customer customer)
    {
        var identity = new Dictionary<string, string>
        {
            ["customerId"] = customer.CustomerId,
            ["fullName"] = customer.FullName
        };

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            identity["email"] = customer.Email;
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            identity["phone"] = customer.Phone;
        }

        return identity;
    }

    private static IReadOnlyDictionary<string, string> BuildPolicy(Policy policy)
    {
        var row = new Dictionary<string, string>
        {
            ["policyId"] = policy.PolicyId,
            ["policyNumber"] = policy.PolicyNumber,
            ["productLine"] = policy.ProductLine
        };

        if (!string.IsNullOrWhiteSpace(policy.CoverType))
        {
            row["coverType"] = policy.CoverType;
        }

        if (policy.EffectiveFrom is { } from)
        {
            row["effectiveFrom"] = from.ToString("O");
        }

        if (policy.EffectiveTo is { } to)
        {
            row["effectiveTo"] = to.ToString("O");
        }

        return row;
    }

    private static IReadOnlyDictionary<string, string> BuildClaim(ClaimCase claim) =>
        new Dictionary<string, string>
        {
            ["caseId"] = claim.CaseId.ToString(),
            ["claimType"] = claim.ClaimType,
            ["productLine"] = claim.ProductLine,
            ["policyNumber"] = claim.PolicyNumber,
            ["status"] = claim.Status.ToString(),
            ["completenessScore"] = claim.CompletenessScore.ToString("0.##"),
            ["riskLevel"] = claim.RiskLevel,
            ["channel"] = claim.Channel,
            ["incidentDate"] = claim.IncidentDate.ToString("O"),
            ["createdAt"] = claim.CreatedAt.ToString("O")
        };

    private static IReadOnlyDictionary<string, string> BuildInteraction(Communication message) =>
        new Dictionary<string, string>
        {
            ["communicationId"] = message.CommunicationId.ToString(),
            ["caseId"] = message.CaseId.ToString(),
            ["channel"] = message.Channel.ToString(),
            ["direction"] = message.Direction,
            ["deliveryStatus"] = message.DeliveryStatus,
            ["messageSnippet"] = Snippet(message.MessageBody),
            ["createdAt"] = message.CreatedAt.ToString("O")
        };

    private static IReadOnlyDictionary<string, string> BuildDocument(CaseDocument document) =>
        new Dictionary<string, string>
        {
            ["documentId"] = document.DocumentId.ToString(),
            ["caseId"] = document.CaseId.ToString(),
            ["fileName"] = document.OriginalFileName,
            ["documentType"] = document.DocumentType.ToString(),
            ["processingStatus"] = document.ProcessingStatus.ToString(),
            ["uploadedAt"] = document.UploadedAt.ToString("O")
        };

    private static CustomerAuditRow ToCustomerAuditRow(DomainAuditEvent auditEvent) =>
        new(
            auditEvent.ActionName,
            auditEvent.ActorType.ToString(),
            auditEvent.UserId,
            "case",
            auditEvent.CaseId?.ToString() ?? string.Empty,
            auditEvent.Timestamp.ToString("O"),
            auditEvent.CorrelationId);

    private static string Snippet(string body) =>
        body.Length <= 160 ? body : body[..160];
}

/// <summary>
/// Channel conversations are ingested by the external-channel pipeline (WhatsApp/webchat/etc.),
/// which is a later program phase. No such data exists yet, so this returns an empty overview
/// honestly rather than simulating conversations. When ingestion is configured this adapter reads
/// the real conversation store.
/// </summary>
public sealed class EfChannelReadRepository : IChannelReadRepository
{
    public Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChannelConversationReadModel>>(Array.Empty<ChannelConversationReadModel>());
}

/// <summary>
/// Sales leads originate from the not-yet-built lead pipeline. Until that data source exists this
/// returns no lead (null) honestly rather than fabricating a lead. The service surfaces this as a
/// 404 for any lead id.
/// </summary>
public sealed class EfSalesLeadReadRepository : ISalesLeadReadRepository
{
    public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
        string tenantId,
        string workspaceId,
        string leadId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<SalesLeadDetailReadModel?>(null);
}
