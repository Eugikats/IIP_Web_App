using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;

namespace InsuranceIntelligence.Application.Customers;

/// <summary>Outcome M: consent as a first class, staff-visible, auditable record. Reads and
/// writes InsuranceIntelligence.Domain.Compliance.ConsentRecord for a named customer, resolving
/// which real-world identifier (email or phone) a purpose's channel implies, rather than asking
/// the caller to know that mapping.</summary>
public sealed class CustomerConsentService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICustomerRepository customerRepository,
    IConsentRepository consentRepository,
    IAuditWriter auditWriter) : ICustomerConsentService
{
    public async Task<IReadOnlyList<CustomerConsentRecordDto>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCustomers, cancellationToken);
        var (scope, customer) = await ResolveCustomerAsync(customerId, cancellationToken);

        var records = new List<ConsentRecord>();
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            records.AddRange(await consentRepository.ListForCustomerAsync(scope, customer.Email.Trim(), cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            records.AddRange(await consentRepository.ListForCustomerAsync(scope, customer.Phone.Trim(), cancellationToken));
        }

        // Also check under the raw CustomerId, the third identifier form ConsentRecord.CustomerIdentifier
        // documents (an identified, policy-matched sender).
        records.AddRange(await consentRepository.ListForCustomerAsync(scope, customer.CustomerId, cancellationToken));

        // "Currently permitted" is a property of the (identifier, purpose) pair, not of any one row
        // in isolation: an old grant later superseded by a withdrawal must not still read as active
        // just because ConsentEnforcementGuard would accept that one row on its own.
        var currentRecordIds = records
            .GroupBy(record => (record.CustomerIdentifier, record.Purpose))
            .Select(group => group.OrderByDescending(record => record.RecordedAt).First().RecordId)
            .ToHashSet();

        return records
            .OrderByDescending(record => record.RecordedAt)
            .Select(record => ToDto(record, currentRecordIds.Contains(record.RecordId)))
            .ToArray();
    }

    public async Task<CustomerConsentRecordDto> RecordExplicitOptInAsync(string customerId, string purpose, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var (scope, customer) = await ResolveCustomerAsync(customerId, cancellationToken);
        var (identifier, channel) = ResolveIdentifierForPurpose(customer, purpose);
        var context = tenantContextAccessor.Current;
        var correlationId = Guid.NewGuid().ToString("N");

        var record = new ConsentRecord
        {
            RecordId = Guid.NewGuid(),
            Scope = scope,
            CustomerIdentifier = identifier,
            Basis = ConsentBasis.ExplicitOptIn,
            Purpose = purpose,
            Channel = channel,
            RecordedByUserId = context.UserId,
            RecordedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            InteractionReference = $"staff:customer:{customer.CustomerId}",
        };

        await consentRepository.SaveAsync(record, cancellationToken);
        await auditWriter.WriteAsync(AuditEvent.ConsentRecorded(record), cancellationToken);
        return ToDto(record, isCurrent: true);
    }

    public async Task<CustomerConsentRecordDto> WithdrawAsync(string customerId, string purpose, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var (scope, customer) = await ResolveCustomerAsync(customerId, cancellationToken);
        var (identifier, channel) = ResolveIdentifierForPurpose(customer, purpose);
        var context = tenantContextAccessor.Current;
        var correlationId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var record = new ConsentRecord
        {
            RecordId = Guid.NewGuid(),
            Scope = scope,
            CustomerIdentifier = identifier,
            Basis = ConsentBasis.Withdrawn,
            Purpose = purpose,
            Channel = channel,
            WithdrawnAt = now,
            RecordedByUserId = context.UserId,
            RecordedAt = now,
            CorrelationId = correlationId,
            InteractionReference = $"staff:customer:{customer.CustomerId}",
        };

        await consentRepository.SaveAsync(record, cancellationToken);
        await auditWriter.WriteAsync(AuditEvent.ConsentRecorded(record), cancellationToken);
        return ToDto(record, isCurrent: true);
    }

    private async Task<(TenantScope Scope, Customer Customer)> ResolveCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new CustomerConsentValidationException("customerId", "Customer ID is required.");
        }

        var context = tenantContextAccessor.Current;
        var customer = await customerRepository.GetAsync(customerId.Trim(), context.Scope, cancellationToken);
        if (customer is null)
        {
            throw new CustomerConsentValidationException("customerId", "Customer was not found for the current tenant workspace.");
        }

        return (context.Scope, customer);
    }

    private static (string Identifier, string Channel) ResolveIdentifierForPurpose(Customer customer, string purpose)
    {
        if (string.Equals(purpose, ConsentPurposes.Email, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(customer.Email)
                ? throw new CustomerConsentValidationException("purpose", "This customer has no email address on file.")
                : (customer.Email.Trim(), "Email");
        }

        if (string.Equals(purpose, ConsentPurposes.Sms, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(customer.Phone)
                ? throw new CustomerConsentValidationException("purpose", "This customer has no phone number on file.")
                : (customer.Phone.Trim(), "SMS");
        }

        if (string.Equals(purpose, ConsentPurposes.WhatsApp, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(customer.Phone)
                ? throw new CustomerConsentValidationException("purpose", "This customer has no phone number on file.")
                : (customer.Phone.Trim(), "WhatsApp");
        }

        throw new CustomerConsentValidationException("purpose", $"Unknown consent purpose '{purpose}'.");
    }

    private static CustomerConsentRecordDto ToDto(ConsentRecord record, bool isCurrent) =>
        new(
            record.RecordId,
            record.Basis.ToString(),
            record.Purpose,
            record.Channel,
            record.RecordedAt,
            record.RecordedByUserId,
            record.WithdrawnAt,
            record.InteractionReference,
            isCurrent && ConsentEnforcementGuard.IsPermitted(record));
}
