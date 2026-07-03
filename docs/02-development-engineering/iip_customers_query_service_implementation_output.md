# IIP Backend Business Logic Output - Customers Query Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Status:** Implementation-ready service output for repository application layer  
**Date:** July 2026  

---

## Shared rules used by this service output

This output follows the same implementation pattern as the corrected `CommandCenterQueryService` logic:

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any read/write.
3. Apply tenantId and workspaceId filters in the repository/query boundary.
4. Return the exact reconciled frontend/backend DTO shape where applicable.
5. Write audit events for state-changing operations and sensitive reads.
6. Never fabricate records when no data exists.
7. Never let an AI/system actor be the final actor for a consequential insurance decision.
8. Surface missing providers as ProviderNotConfiguredException or ProviderUnavailableException.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, and ASP.NET Core.
```



## 1. Scope of this pass

This pass implements **Customer 360 read logic**:

```text
ICustomerQueryService / CustomersQueryService
```

Implemented capabilities:

```text
- Return CustomerProfileDto matching backend spec Section 16A.6 and frontend CustomerProfile.
- Enforce customers.read permission. If missing, add `Permissions.CustomersRead = "customers.read"`.
- Filter every section by tenant/workspace.
- Use best-effort dictionary sections for Customer 360 because field-level schema is not finalized.
- Write audit event for sensitive customer profile read.
- Return empty arrays/dictionaries for missing sections; do not fabricate data.
```

Not implemented in this pass:

```text
- Customer account settings or customer login.
- Customer profile mutation.
- Payment provider integration.
- Detailed field-level Customer 360 schema.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Customers/ICustomerQueryService.cs
src/InsuranceIntelligence.Application/Customers/CustomerQueryService.cs
src/InsuranceIntelligence.Application/Customers/ICustomerReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Customers/CustomerQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Customers/CustomersContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Customers;

public interface ICustomerQueryService
{
    Task<CustomerProfileDto?> GetCustomerProfileAsync(
        string customerId,
        CancellationToken cancellationToken);
}
```

---

## 4. Repository port

```csharp
namespace InsuranceIntelligence.Application.Customers;

public interface ICustomerReadRepository
{
    Task<CustomerProfileReadModel?> GetCustomerProfileAsync(
        string tenantId,
        string? workspaceId,
        string customerId,
        CancellationToken cancellationToken);
}

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
```

---

## 5. Implementation

```csharp
namespace InsuranceIntelligence.Application.Customers;

internal sealed class CustomerQueryService : ICustomerQueryService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ICustomerReadRepository _repository;
    private readonly IAuditWriter _auditWriter;

    public CustomerQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ICustomerReadRepository repository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<CustomerProfileDto?> GetCustomerProfileAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new ValidationException("customerId is required.");

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.CustomersRead, cancellationToken);

        var profile = await _repository.GetCustomerProfileAsync(context.TenantId, context.WorkspaceId, customerId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: "data_viewed",
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: "customer_profile_viewed",
            EntityType: "customer",
            EntityId: customerId,
            Summary: "Customer profile viewed.",
            Reason: null,
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), cancellationToken);

        return new CustomerProfileDto(
            CustomerId: profile.CustomerId,
            Identity: profile.Identity,
            Policies: profile.Policies,
            Claims: profile.Claims,
            Interactions: profile.Interactions,
            Documents: profile.Documents,
            Consent: profile.Consent,
            Payments: profile.Payments,
            Opportunities: profile.Opportunities,
            AuditHistory: profile.AuditHistory);
    }
}
```

---

## 6. Tests to add

```text
CustomerQueryServiceTests
- GetCustomerProfileAsync_RequiresCustomersReadPermission
- GetCustomerProfileAsync_ReturnsOnlyTenantWorkspaceCustomer
- GetCustomerProfileAsync_WritesSensitiveReadAudit
- GetCustomerProfileAsync_ReturnsNullWhenNotFoundOrNotVisible
- GetCustomerProfileAsync_DoesNotExposeRawProviderPayloadsOrHiddenRiskNotes
```

Contract test:

```text
CustomersContractMatchesFrontendTypesTest
- CustomerProfileDto serializes to customerId, identity, policies, claims, interactions, documents, consent, payments, opportunities, auditHistory
- dictionary section values serialize as object maps and arrays of object maps
```

Provider failure test:

```text
Not applicable. Customer query reads canonical data only. External policy/payment provider failures belong to integration/adapters, not this read model.
```

---

## 7. Completion report template

```text
Service implemented: CustomersQueryService.
Logic implemented: customer profile read with tenant/workspace scope, customers.read, sensitive-read audit.
TODOs: Confirm detailed Customer 360 field-level schema for identity, policies, claims, payments, and opportunities.
Missing dependency: add Permissions.CustomersRead if not present.
No fake data: Confirmed.
Recommended next service: Sales & Leads query service.
```
