# IIP Backend Business Logic Output - Sales & Leads Query Service

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

This pass implements **Sales & Leads detail read logic**:

```text
ISalesLeadQueryService / SalesLeadQueryService
```

Implemented capabilities:

```text
- Return SalesLeadDetailDto matching backend spec Section 16A.7 and frontend SalesLeadDetail.
- Enforce sales_leads.read permission. If missing, add `Permissions.SalesLeadsRead = "sales_leads.read"`.
- Filter by tenant/workspace.
- Exclude hidden scoring inputs, raw transcripts, provider metadata, and model notes.
- Return null if the lead is not found or not visible.
```

Not implemented in this pass:

```text
- Lead creation.
- Quote generation.
- Customer communication send.
- Conversion workflow transitions.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/SalesLeads/ISalesLeadQueryService.cs
src/InsuranceIntelligence.Application/SalesLeads/SalesLeadQueryService.cs
src/InsuranceIntelligence.Application/SalesLeads/ISalesLeadReadRepository.cs
src/InsuranceIntelligence.Tests/Application/SalesLeads/SalesLeadQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/SalesLeads/SalesLeadsContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.SalesLeads;

public interface ISalesLeadQueryService
{
    Task<SalesLeadDetailDto?> GetLeadDetailAsync(
        string leadId,
        CancellationToken cancellationToken);
}
```

---

## 4. Repository port

```csharp
namespace InsuranceIntelligence.Application.SalesLeads;

public interface ISalesLeadReadRepository
{
    Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
        string tenantId,
        string? workspaceId,
        string leadId,
        CancellationToken cancellationToken);
}

public sealed record SalesLeadDetailReadModel(
    string LeadId,
    string LeadSource,
    string CustomerNeed,
    string ConversationSummary,
    string ProductFit,
    string QuoteStatus,
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<string> FollowUpActions,
    string? AssignedSalesOfficer,
    decimal? ConversionProbability);
```

---

## 5. Implementation

```csharp
namespace InsuranceIntelligence.Application.SalesLeads;

internal sealed class SalesLeadQueryService : ISalesLeadQueryService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ISalesLeadReadRepository _repository;

    public SalesLeadQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ISalesLeadReadRepository repository)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
    }

    public async Task<SalesLeadDetailDto?> GetLeadDetailAsync(string leadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leadId)) throw new ValidationException("leadId is required.");

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.SalesLeadsRead, cancellationToken);

        var lead = await _repository.GetLeadDetailAsync(context.TenantId, context.WorkspaceId, leadId, cancellationToken);
        if (lead is null)
        {
            return null;
        }

        return new SalesLeadDetailDto(
            LeadId: lead.LeadId,
            LeadSource: lead.LeadSource,
            CustomerNeed: lead.CustomerNeed,
            ConversationSummary: lead.ConversationSummary,
            ProductFit: lead.ProductFit,
            QuoteStatus: lead.QuoteStatus,
            RequiredInformation: lead.RequiredInformation,
            FollowUpActions: lead.FollowUpActions,
            AssignedSalesOfficer: lead.AssignedSalesOfficer,
            ConversionProbability: lead.ConversionProbability);
    }
}
```

---

## 6. Tests to add

```text
SalesLeadQueryServiceTests
- GetLeadDetailAsync_RequiresSalesLeadsReadPermission
- GetLeadDetailAsync_ReturnsOnlyTenantWorkspaceLead
- GetLeadDetailAsync_ReturnsNullWhenNotFoundOrNotVisible
- GetLeadDetailAsync_DoesNotExposeHiddenScoringInputsOrRawTranscripts
```

Contract test:

```text
SalesLeadsContractMatchesFrontendTypesTest
- SalesLeadDetailDto serializes to leadId, leadSource, customerNeed, conversationSummary, productFit, quoteStatus, requiredInformation, followUpActions, assignedSalesOfficer, conversionProbability
```

Provider failure test:

```text
Not applicable. Query reads canonical data only.
```

Human-decision guard test:

```text
Not applicable. This read service does not bind policies, set premiums, or make underwriting decisions.
```

---

## 7. Completion report template

```text
Service implemented: SalesLeadQueryService.
Logic implemented: tenant/workspace-scoped sales lead detail read.
TODOs: Confirm quoteStatus canonical value set.
Missing dependency: add Permissions.SalesLeadsRead if not present.
No fake data: Confirmed.
Recommended next service: IAuditQueryService.
```
