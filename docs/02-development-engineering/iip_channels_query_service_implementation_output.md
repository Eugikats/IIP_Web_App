# IIP Backend Business Logic Output - Channels Query Service

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

This pass implements **Channels overview read logic**:

```text
IChannelQueryService / ChannelsQueryService
```

Implemented capabilities:

```text
- Return channel inbox rows matching `ChannelConversationRowDto`.
- Enforce channels.read permission. If missing, add `Permissions.ChannelsRead = "channels.read"`.
- Filter by tenant/workspace from ITenantContextAccessor.
- Exclude raw message bodies and provider payloads from list rows.
- Return empty list if no channel conversations exist.
```

Not implemented in this pass:

```text
- Sending messages.
- Channel provider ingestion.
- WhatsApp/Graph/webchat adapters.
- Customer-facing login or portal.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Channels/IChannelQueryService.cs
src/InsuranceIntelligence.Application/Channels/ChannelQueryService.cs
src/InsuranceIntelligence.Application/Channels/ChannelQueryModels.cs
src/InsuranceIntelligence.Application/Channels/IChannelReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Channels/ChannelQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Channels/ChannelsContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Channels;

public interface IChannelQueryService
{
    Task<IReadOnlyList<ChannelConversationRowDto>> GetOverviewAsync(
        CancellationToken cancellationToken);
}
```

---

## 4. Repository port

```csharp
namespace InsuranceIntelligence.Application.Channels;

public interface IChannelReadRepository
{
    Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);
}

public sealed record ChannelConversationReadModel(
    string Id,
    string CustomerName,
    string Channel,
    string DetectedIntent,
    string SuggestedRouting,
    decimal AiConfidence,
    string Status,
    string? AssignedTo,
    string LastMessageSnippet,
    DateTimeOffset LastActivityAt);
```

---

## 5. Implementation

```csharp
namespace InsuranceIntelligence.Application.Channels;

internal sealed class ChannelQueryService : IChannelQueryService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IChannelReadRepository _repository;

    public ChannelQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IChannelReadRepository repository)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
    }

    public async Task<IReadOnlyList<ChannelConversationRowDto>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ChannelsRead, cancellationToken);

        var rows = await _repository.GetOverviewAsync(context.TenantId, context.WorkspaceId, cancellationToken);

        return rows.Select(r => new ChannelConversationRowDto(
            Id: r.Id,
            CustomerName: r.CustomerName,
            Channel: NormalizeChannel(r.Channel),
            DetectedIntent: NormalizeIntent(r.DetectedIntent),
            SuggestedRouting: r.SuggestedRouting,
            AiConfidence: r.AiConfidence,
            Status: r.Status,
            AssignedTo: r.AssignedTo,
            LastMessage: r.LastMessageSnippet,
            LastActivityAt: r.LastActivityAt.ToString("O")))
            .ToList();
    }

    private static string NormalizeChannel(string value) => value switch
    {
        ChannelValues.WhatsApp => ChannelValues.WhatsApp,
        ChannelValues.WebChat => ChannelValues.WebChat,
        ChannelValues.Sms => ChannelValues.Sms,
        ChannelValues.Email => ChannelValues.Email,
        ChannelValues.CallCenter => ChannelValues.CallCenter,
        ChannelValues.Broker => ChannelValues.Broker,
        ChannelValues.WebsiteForm => ChannelValues.WebsiteForm,
        _ => ChannelValues.Email // TODO: confirm fallback behavior; repository should ideally store canonical values only.
    };

    private static string NormalizeIntent(string value) => value switch
    {
        ChannelDetectedIntentValues.ClaimFnol => ChannelDetectedIntentValues.ClaimFnol,
        ChannelDetectedIntentValues.QuoteRequest => ChannelDetectedIntentValues.QuoteRequest,
        ChannelDetectedIntentValues.Renewal => ChannelDetectedIntentValues.Renewal,
        ChannelDetectedIntentValues.Complaint => ChannelDetectedIntentValues.Complaint,
        ChannelDetectedIntentValues.PolicyInquiry => ChannelDetectedIntentValues.PolicyInquiry,
        ChannelDetectedIntentValues.DocumentUpload => ChannelDetectedIntentValues.DocumentUpload,
        ChannelDetectedIntentValues.PaymentInquiry => ChannelDetectedIntentValues.PaymentInquiry,
        ChannelDetectedIntentValues.GeneralService => ChannelDetectedIntentValues.GeneralService,
        _ => ChannelDetectedIntentValues.GeneralService
    };
}
```

---

## 6. Tests to add

```text
ChannelQueryServiceTests
- GetOverviewAsync_RequiresChannelsReadPermission
- GetOverviewAsync_ReturnsOnlyTenantWorkspaceRows
- GetOverviewAsync_ReturnsEmptyWhenNoRowsExist
- GetOverviewAsync_DoesNotExposeRawMessageBodyOrProviderIds
- GetOverviewAsync_UsesIsoLastActivityAt
```

Contract test:

```text
ChannelsContractMatchesFrontendTypesTest
- ChannelConversationRowDto serializes to id, customerName, channel, detectedIntent, suggestedRouting, aiConfidence, status, assignedTo, lastMessage, lastActivityAt
- channel and detectedIntent values match frontend union
```

Provider failure test:

```text
Not applicable. Query reads canonical database rows only. Ingestion provider failures belong to channel ingestion services.
```

---

## 7. Completion report template

```text
Service implemented: ChannelsQueryService.
Logic implemented: tenant/workspace-scoped channel overview with safe snippets only.
TODOs: Confirm channel conversation status value set.
Missing dependency: add Permissions.ChannelsRead if not present.
No fake data: Confirmed.
Recommended next service: Customers query service.
```
