using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.Customers;

public interface ICustomerQueryService
{
    Task<CustomerProfileDto?> GetCustomerProfileAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerListItemDto>> ListCustomersAsync(
        CancellationToken cancellationToken = default);
}

public interface ICustomerReadRepository
{
    Task<CustomerProfileReadModel?> GetCustomerProfileAsync(
        string tenantId,
        string workspaceId,
        string customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerListItemDto>> ListCustomersAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record CustomerListItemDto(
    string CustomerId,
    string FullName,
    string? Email,
    string? Phone,
    int ActiveClaims);

public sealed record CustomerProfileReadModel(
    string CustomerId,
    IReadOnlyDictionary<string, string> Identity,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Policies,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Claims,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Interactions,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Documents,
    IReadOnlyDictionary<string, string> Consent,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Payments,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Opportunities,
    IReadOnlyList<AuditEventRowDto> AuditHistory);

public sealed record CustomerProfileDto(
    string CustomerId,
    IReadOnlyDictionary<string, string> Identity,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Policies,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Claims,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Interactions,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Documents,
    IReadOnlyDictionary<string, string> Consent,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Payments,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Opportunities,
    IReadOnlyList<AuditEventRowDto> AuditHistory);

public sealed record AuditEventRowDto(
    string ActionName,
    string ActorType,
    string? ActorUserId,
    string TargetType,
    string TargetId,
    string Timestamp,
    string CorrelationId);

public sealed class CustomerQueryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
