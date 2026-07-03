using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.Customers;

public sealed class CustomerQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICustomerReadRepository repository,
    IAuditWriter auditWriter) : ICustomerQueryService
{
    private static readonly string[] UnsafeDictionaryKeyFragments =
    [
        "raw",
        "providerpayload",
        "hiddenrisk",
        "internalrisk",
        "fraudsignal",
        "reserve",
        "prompt",
        "systemmessage"
    ];

    public async Task<CustomerProfileDto?> GetCustomerProfileAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new CustomerQueryValidationException("customerId", "Customer ID is required.");
        }

        await permissionService.RequirePermissionAsync(Permission.ViewCustomers, cancellationToken);

        var context = tenantContextAccessor.Current;
        var profile = await repository.GetCustomerProfileAsync(
            context.TenantId,
            context.WorkspaceId,
            customerId.Trim(),
            cancellationToken);

        if (profile is null)
        {
            return null;
        }

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: null,
                UserId: context.UserId,
                ActorType.User,
                "customer.profile.viewed",
                DateTimeOffset.UtcNow,
                BeforeStatus: null,
                AfterStatus: null,
                EvidenceIds: [profile.CustomerId],
                ModelProvider: "CustomerQuery",
                Confidence: null,
                ValidationResult: "viewed",
                ApprovalId: null,
                AuditReason: "Sensitive customer profile read.",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        return new CustomerProfileDto(
            profile.CustomerId,
            SanitizeDictionary(profile.Identity),
            SanitizeDictionaryList(profile.Policies),
            SanitizeDictionaryList(profile.Claims),
            SanitizeDictionaryList(profile.Interactions),
            SanitizeDictionaryList(profile.Documents),
            SanitizeDictionary(profile.Consent),
            SanitizeDictionaryList(profile.Payments),
            SanitizeDictionaryList(profile.Opportunities),
            profile.AuditHistory);
    }

    private static IReadOnlyDictionary<string, string> SanitizeDictionary(
        IReadOnlyDictionary<string, string> values)
    {
        return values
            .Where(entry => IsSafeDictionaryKey(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> SanitizeDictionaryList(
        IReadOnlyList<IReadOnlyDictionary<string, string>> values) =>
        values.Select(SanitizeDictionary).ToArray();

    private static bool IsSafeDictionaryKey(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return !UnsafeDictionaryKeyFragments.Any(normalized.Contains);
    }
}
