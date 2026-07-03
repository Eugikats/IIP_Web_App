# IIP Backend Wiring — Controllers, Composition Root, and Request Pipeline

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready backend wiring specification  
**Layer:** API / composition root / request pipeline  
**Status:** Ready for new-repository implementation  
**Date:** July 2026

---

## Reconciliation changelog

- **Item 1 — RuntimeProfile ownership fixed:** Document A now owns the canonical `RuntimeProfile` enum, `RuntimeProfileParser.Parse(string)`, and `RuntimeProfileExtensions.IsProtected()` definitions; this document now parses the string returned by `AzureProviderStartupValidator.ResolveRuntimeProfile(...)` instead of assuming it is already an enum.
- **Item 2 — Tenant context pipeline fixed:** Removed Document B's competing `TenantContextPopulationMiddleware`, `ITenantContextWriter`, `HttpTenantContextAccessor`, and claim-owned tenant resolution; this document now uses Document A's `TenantWorkspaceContextMiddleware`, `IAuthenticatedTenantResolver`, and `AuthenticatedTenantResolver` unchanged.
- **Item 3 — Dangling permission resolver removed:** Removed the undefined third permission-resolver abstraction from Document B; permission resolution is handled by Document A's `IPermissionReadRepository` and `PermissionService`.
- **Item 4 — InfrastructureOptions field drift fixed:** Replaced undefined nested references such as `options.Entra.Authority`, `options.Entra.Audience`, `options.Entra.Issuer`, `options.SqlServer.ConnectionString`, and `options.AzureOpenAi` with Document A / benchmark-compatible fields: `EntraTenantId`, `EntraAudience`, `EntraIssuer`, `SqlServerConnectionString`, and `AzureOpenAI`.
- **Item 5 — LocalTest project naming fixed:** Standardized isolated test-double references on `InsuranceIntelligence.LocalTestDoubles`; `InsuranceIntelligence.Api.LocalTestHost` remains a separate standalone host that may reference `LocalTestDoubles` and must never be referenced by the real API project.
- **Item 6 — Multitenant Entra authentication corrected:** Replaced single-insurer-tenant JWT validation with Microsoft Entra `organizations` authority mode for IIP's single multitenant app registration. `EntraTenantId` is now documented as IIP's publisher/home tenant, not a customer insurer tenant, and issuer validation is delegated to the multitenant-aware Microsoft.Identity.Web / IdentityModel path instead of hardcoding `ValidIssuer`.

---

## Shared rules used by this wiring output

This document wires already-specified Application-layer services and Infrastructure-layer ports to an authenticated ASP.NET Core HTTP surface. It does **not** redesign the twelve service specifications and does **not** redesign the four core ports.

```text
1. Resolve tenant/workspace/user context server-side from the authenticated principal.
2. Never trust tenant ID, workspace ID, role, or permissions from route parameters, query strings, request bodies, browser state, or client headers.
3. Every controller action must enforce the required permission before invoking the Application service.
4. Every Application service must still enforce its own permission and tenant/workspace rules as defense in depth.
5. Protected runtime profiles must fail closed during startup when provider, database, Entra, or runtime configuration is incomplete or unsafe.
6. Do not expose mock, simulated, placeholder, in-memory, seeded-demo, or fallback provider behavior in Staging, Production, or ManagedApplicationReviewer.
7. AI-touching endpoints must expose HumanReviewRequirementDto where returned by the service and must not expose an AI-only final-decision path.
8. Controllers must be thin adapters only: bind route/query/body values, enforce permission, call service, return service DTOs.
9. Exception-to-HTTP mapping must be centralized through one middleware/filter, not copied into each controller.
10. Errors must include a correlation ID for support triage and must not include stack traces, provider payloads, secrets, prompts, tokens, or raw exception bodies.
11. Empty data remains empty. No controller may fabricate operational records, fake metrics, fake documents, fake approvals, or fake success responses.
12. LocalTest-only implementations must live in `InsuranceIntelligence.LocalTestDoubles`, a physically separate project/assembly that is never referenced by the production API project.
```

---

## 1. Scope of this pass

This pass implements the **HTTP wiring layer** for the already-specified IIP backend services:

```text
CommandCenterQueryService
ClaimsQueryService
SlaRiskService
ApprovalQueryService
ApprovalDecisionService
DocumentUploadService
DocumentExtractionWorkflowService
DocumentReviewService
CompletenessCheckService
ChannelQueryService
CustomerQueryService
SalesLeadQueryService
AuditQueryService
```

It also wires the already-specified core Infrastructure ports:

```text
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
```

Implemented in this pass:

```text
- ASP.NET Core controllers for Command Center, Claims, Approvals, Documents, Channels, Customers, Sales & Leads, and Audit.
- Route templates for every externally exposed method from the service outputs.
- Entra JWT authentication setup for real runtimes.
- Development authentication guardrails for non-protected local development only.
- Scoped tenant context population from authenticated principal.
- Program.cs / DI composition root registration order.
- Protected-runtime startup validation using AzureProviderStartupValidator.ThrowIfProductionInvalid.
- Global exception handling middleware.
- OpenAPI generation and protected Swagger UI gating.
- Integration/security tests for request pipeline correctness.
```

Not implemented in this pass:

```text
- Application service business logic.
- Infrastructure port implementations.
- EF Core repositories.
- Database migrations.
- Frontend code.
- Azure Bicep / deployment packaging.
- LocalTest doubles themselves, except their required isolation and host wiring rules.
```

---

## 2. Repository structure decision

### Decision

The new repository **must introduce a dedicated `InsuranceIntelligence.Application` project**.

Reason:

```text
All twelve Application-layer service-output docs already assume namespaces such as:
InsuranceIntelligence.Application.Claims
InsuranceIntelligence.Application.Documents
InsuranceIntelligence.Application.Approvals
InsuranceIntelligence.Application.CommandCenter
```

The new repository should not copy the current flatter “API hosts everything” shape. The new build should use clear layer separation and must consume Document A's core ports from `InsuranceIntelligence.Application.Common.*` and their production Infrastructure implementations.

### Required project layout

```text
src/
  InsuranceIntelligence.Api/
    Controllers/
    Middleware/
    Extensions/
    DependencyInjection/
    Program.cs

  InsuranceIntelligence.Application/
    CommandCenter/
    Claims/
    Documents/
    Approvals/
    Channels/
    Customers/
    SalesLeads/
    Audit/
    Common/

  InsuranceIntelligence.Domain/
    Approvals/
    Audit/
    Common/

  InsuranceIntelligence.Infrastructure/
    Configuration/
    Persistence/
    Repositories/
    Providers/
    Security/
    Extensions/

  InsuranceIntelligence.LocalTestDoubles/
    Ports/
    Providers/
    Extensions/

  InsuranceIntelligence.Api.LocalTestHost/
    Program.cs

  InsuranceIntelligence.Tests/
    Api/
    Application/
    Infrastructure/
    Security/
```

### Project-reference rules

```text
InsuranceIntelligence.Api -> Application, Infrastructure, Domain
InsuranceIntelligence.Infrastructure -> Application, Domain
InsuranceIntelligence.Application -> Domain
InsuranceIntelligence.Domain -> no Application/API/Infrastructure dependency
InsuranceIntelligence.LocalTestDoubles -> Application, Domain only where needed
InsuranceIntelligence.Api.LocalTestHost -> Api, Application, Infrastructure, LocalTestDoubles
InsuranceIntelligence.Api must NOT reference InsuranceIntelligence.LocalTestDoubles
InsuranceIntelligence.Application must NOT reference InsuranceIntelligence.LocalTestDoubles
InsuranceIntelligence.Infrastructure must NOT reference InsuranceIntelligence.LocalTestDoubles
InsuranceIntelligence.Functions must NOT reference InsuranceIntelligence.LocalTestDoubles
```

This is the compile-time guarantee that LocalTest-only doubles cannot be published with the real API. The standalone `InsuranceIntelligence.Api.LocalTestHost` may reference `InsuranceIntelligence.LocalTestDoubles`, but the real `InsuranceIntelligence.Api` project must never reference it.

## 3. Route map

The API route prefix for this wiring pass is:

```text
/api
```

If a future API versioning policy is approved, add versioning through a route group or API versioning package in one change. Do not silently mix `/api` and `/api/v1` in this pass.

### 3.1 Command Center

| Method | Route | Service method | Permission |
|---|---|---|---|
| `POST` | `/api/command-center/query` | `ICommandCenterQueryService.QueryAsync` | `command_center.query` |

### 3.2 Claims and SLA

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/claims` | `IClaimsQueryService.ListClaimsAsync` | `claims.read` |
| `GET` | `/api/claims/{claimId}` | `IClaimsQueryService.GetClaimDetailAsync` | `claims.read` |
| `GET` | `/api/claims/attention` | `IClaimsQueryService.GetClaimsNeedingAttentionAsync` | `claims.read` |
| `GET` | `/api/claims/daily-summary` | `IClaimsQueryService.GetDailySummaryAsync` | `claims.read` |
| `GET` | `/api/claims/{claimId}/draft-context` | `IClaimsQueryService.GetClaimForDraftAsync` | `claims.read` |
| `GET` | `/api/claims/sla-risks` | `ISlaRiskService.GetAtRiskItemsAsync` | `claims.read` |
| `GET` | `/api/claims/{claimId}/sla-status` | `ISlaRiskService.CalculateSlaStatusAsync` | `claims.read` |

`/api/claims/{claimId}/draft-context` is sensitive and exists only for authorized staff/API workflows. It must never expose internal notes, risk flags, reserves, prompts, provider payloads, or hidden decision metadata.

### 3.3 Approvals

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/approvals/queue` | `IApprovalQueryService.GetPendingApprovalsAsync` | `approvals.read` |
| `POST` | `/api/approvals/{approvalId}/decisions` | `IApprovalDecisionService.DecideAsync` | `approvals.review` |

The decision route must construct `DecisionActorType.Human` server-side from the authenticated user context. The client must not be allowed to submit `ActorType = Agent`, `System`, or `Integration`.

### 3.4 Documents

| Method | Route | Service method | Permission |
|---|---|---|---|
| `POST` | `/api/documents/uploads` | `IDocumentUploadService.RegisterUploadAsync` | `documents.upload` |
| `POST` | `/api/documents/{documentId}/extraction` | `IDocumentExtractionWorkflowService.ProcessExtractionAsync` | `documents.review` |
| `GET` | `/api/documents/{documentId}/fields` | `IDocumentReviewService.GetExtractedFieldsAsync` | `documents.review` |
| `PATCH` | `/api/documents/{documentId}/fields/{fieldId}/correction` | `IDocumentReviewService.CorrectFieldAsync` | `documents.review` |
| `PATCH` | `/api/documents/{documentId}/fields/{fieldId}/status` | `IDocumentReviewService.SetFieldStatusAsync` | `documents.review` |
| `GET` | `/api/documents/missing` | `ICompletenessCheckService.GetMissingDocumentsAsync` | `documents.review` |
| `POST` | `/api/claims/{claimId}/documents/completeness-check` | `ICompletenessCheckService.CheckClaimCompletenessAsync` | `documents.review` |

### 3.5 Channels

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/channels/overview` | `IChannelQueryService.GetOverviewAsync` | `channels.read` |

### 3.6 Customers

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/customers/{customerId}` | `ICustomerQueryService.GetCustomerProfileAsync` | `customers.read` |

### 3.7 Sales & Leads

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/sales-leads/{leadId}` | `ISalesLeadQueryService.GetLeadDetailAsync` | `sales_leads.read` |

### 3.8 Audit

| Method | Route | Service method | Permission |
|---|---|---|---|
| `GET` | `/api/audit/events` | `IAuditQueryService.ListEventsAsync` | `audit.read` |

---

## 4. Standard API response and error body

Controllers may return service DTOs directly during early implementation. The production-preferred shape is an envelope so correlation metadata is consistent.

```csharp
namespace InsuranceIntelligence.Api.Contracts;

public sealed record ApiResponse<T>(
    T Data,
    ApiResponseMeta Meta);

public sealed record ApiResponseMeta(
    string CorrelationId,
    string TenantId,
    string? WorkspaceId,
    DateTimeOffset GeneratedAt);

public sealed record ApiErrorResponse(ApiError Error);

public sealed record ApiError(
    string Code,
    string Message,
    string CorrelationId,
    string? Target = null,
    object? Details = null);

public sealed record NotConfiguredBody(
    string Capability,
    string Message,
    string? AdminHref,
    string CorrelationId);
```

Provider errors must return `503` and include a safe `NotConfiguredBody` or `ApiErrorResponse`. Do not return raw Azure SDK exceptions, provider response bodies, secrets, endpoints, keys, or stack traces.

---

## 5. Controller layer

Controllers are thin adapters. They must:

```text
- Require authentication.
- Enforce the endpoint permission before calling the service.
- Bind route/query/body values into the service request records already defined in service docs.
- Never accept tenantId/workspaceId/role/permission from the request as authority.
- Never catch exceptions for HTTP mapping; the global middleware handles that.
```

### 5.1 Base controller helper

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[ApiController]
[Authorize]
public abstract class IipControllerBase : ControllerBase
{
    protected static ActionResult<T> OkData<T>(T data) => new OkObjectResult(data);
}
```

The base controller intentionally does not expose tenant or workspace IDs. Tenant context is resolved by middleware into `ITenantContextAccessor`.

### 5.2 CommandCenterController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.CommandCenter.Models;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/command-center")]
public sealed class CommandCenterController : IipControllerBase
{
    private readonly ICommandCenterQueryService _queryService;
    private readonly IPermissionService _permissionService;

    public CommandCenterController(
        ICommandCenterQueryService queryService,
        IPermissionService permissionService)
    {
        _queryService = queryService;
        _permissionService = permissionService;
    }

    [HttpPost("query")]
    public async Task<ActionResult<CommandCenterQueryResponse>> QueryAsync(
        [FromBody] CommandCenterQueryRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.CommandCenterQuery, cancellationToken);
        var response = await _queryService.QueryAsync(request, cancellationToken);
        return Ok(response);
    }
}
```

`CommandCenterQueryResponse` includes `HumanReviewRequirementDto`. The controller must return it unchanged. Do not add any route that lets the caller approve, reject, send, bind, settle, pay, or finalize a consequential decision through this AI path.

### 5.3 ClaimsController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.CommandCenter.Models;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/claims")]
public sealed class ClaimsController : IipControllerBase
{
    private readonly IClaimsQueryService _claimsQueryService;
    private readonly ISlaRiskService _slaRiskService;
    private readonly IPermissionService _permissionService;

    public ClaimsController(
        IClaimsQueryService claimsQueryService,
        ISlaRiskService slaRiskService,
        IPermissionService permissionService)
    {
        _claimsQueryService = claimsQueryService;
        _slaRiskService = slaRiskService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClaimAttentionRowDto>>> ListAsync(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? slaStatus,
        [FromQuery] string? assignedTo,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);

        var result = await _claimsQueryService.ListClaimsAsync(
            new ClaimsListQuery(status, priority, slaStatus, assignedTo, search, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("attention")]
    public async Task<ActionResult<IReadOnlyList<ClaimAttentionRowDto>>> AttentionAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return Ok(await _claimsQueryService.GetClaimsNeedingAttentionAsync(cancellationToken));
    }

    [HttpGet("daily-summary")]
    public async Task<ActionResult<ClaimsDailySummaryDto>> DailySummaryAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return Ok(await _claimsQueryService.GetDailySummaryAsync(cancellationToken));
    }

    [HttpGet("sla-risks")]
    public async Task<ActionResult<IReadOnlyList<SlaRiskItem>>> SlaRisksAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return Ok(await _slaRiskService.GetAtRiskItemsAsync(cancellationToken));
    }

    [HttpGet("{claimId}")]
    public async Task<ActionResult<ClaimDetailDto?>> DetailAsync(
        [FromRoute] string claimId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        var result = await _claimsQueryService.GetClaimDetailAsync(claimId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{claimId}/draft-context")]
    public async Task<ActionResult<ClaimForDraftDto?>> DraftContextAsync(
        [FromRoute] string claimId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        var result = await _claimsQueryService.GetClaimForDraftAsync(claimId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{claimId}/sla-status")]
    public async Task<ActionResult<string>> SlaStatusAsync(
        [FromRoute] string claimId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return Ok(await _slaRiskService.CalculateSlaStatusAsync(claimId, cancellationToken));
    }
}
```

### 5.4 ApprovalsController

```csharp
using InsuranceIntelligence.Application.Approvals;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Domain.Approvals;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/approvals")]
public sealed class ApprovalsController : IipControllerBase
{
    private readonly IApprovalQueryService _queryService;
    private readonly IApprovalDecisionService _decisionService;
    private readonly IPermissionService _permissionService;

    public ApprovalsController(
        IApprovalQueryService queryService,
        IApprovalDecisionService decisionService,
        IPermissionService permissionService)
    {
        _queryService = queryService;
        _decisionService = decisionService;
        _permissionService = permissionService;
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<ApprovalQueueRowDto>>> QueueAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ApprovalsRead, cancellationToken);
        return Ok(await _queryService.GetPendingApprovalsAsync(cancellationToken));
    }

    [HttpPost("{approvalId}/decisions")]
    public async Task<ActionResult<ApprovalDecisionResult>> DecideAsync(
        [FromRoute] string approvalId,
        [FromBody] ApprovalDecisionHttpRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ApprovalsReview, cancellationToken);

        var serviceRequest = new ApprovalDecisionRequest(
            ApprovalId: approvalId,
            Decision: request.Decision,
            Reason: request.Reason,
            ActorType: DecisionActorType.Human);

        return Ok(await _decisionService.DecideAsync(serviceRequest, cancellationToken));
    }
}

public sealed record ApprovalDecisionHttpRequest(
    string Decision,
    string? Reason);
```

The HTTP request model intentionally excludes `ActorType`. The server supplies `DecisionActorType.Human` after authentication and permission enforcement.

### 5.5 DocumentsController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.CommandCenter.Models;
using InsuranceIntelligence.Application.Documents;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api")]
public sealed class DocumentsController : IipControllerBase
{
    private readonly IDocumentUploadService _uploadService;
    private readonly IDocumentExtractionWorkflowService _extractionService;
    private readonly IDocumentReviewService _reviewService;
    private readonly ICompletenessCheckService _completenessService;
    private readonly IPermissionService _permissionService;

    public DocumentsController(
        IDocumentUploadService uploadService,
        IDocumentExtractionWorkflowService extractionService,
        IDocumentReviewService reviewService,
        ICompletenessCheckService completenessService,
        IPermissionService permissionService)
    {
        _uploadService = uploadService;
        _extractionService = extractionService;
        _reviewService = reviewService;
        _completenessService = completenessService;
        _permissionService = permissionService;
    }

    [HttpPost("documents/uploads")]
    public async Task<ActionResult<DocumentUploadRegistrationResult>> RegisterUploadAsync(
        [FromBody] DocumentUploadRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsUpload, cancellationToken);
        return Ok(await _uploadService.RegisterUploadAsync(request, cancellationToken));
    }

    [HttpPost("documents/{documentId}/extraction")]
    public async Task<ActionResult<IReadOnlyList<ExtractedFieldDto>>> ExtractAsync(
        [FromRoute] string documentId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return Ok(await _extractionService.ProcessExtractionAsync(documentId, cancellationToken));
    }

    [HttpGet("documents/{documentId}/fields")]
    public async Task<ActionResult<IReadOnlyList<ExtractedFieldDto>>> FieldsAsync(
        [FromRoute] string documentId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return Ok(await _reviewService.GetExtractedFieldsAsync(documentId, cancellationToken));
    }

    [HttpPatch("documents/{documentId}/fields/{fieldId}/correction")]
    public async Task<ActionResult<ExtractedFieldDto>> CorrectFieldAsync(
        [FromRoute] string documentId,
        [FromRoute] string fieldId,
        [FromBody] CorrectFieldHttpRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        return Ok(await _reviewService.CorrectFieldAsync(
            new CorrectExtractedFieldRequest(documentId, fieldId, request.CorrectedValue, request.Reason),
            cancellationToken));
    }

    [HttpPatch("documents/{documentId}/fields/{fieldId}/status")]
    public async Task<ActionResult<ExtractedFieldDto>> SetFieldStatusAsync(
        [FromRoute] string documentId,
        [FromRoute] string fieldId,
        [FromBody] SetFieldStatusHttpRequest request,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        return Ok(await _reviewService.SetFieldStatusAsync(
            new SetExtractedFieldStatusRequest(documentId, fieldId, request.Status, request.Reason),
            cancellationToken));
    }

    [HttpGet("documents/missing")]
    public async Task<ActionResult<IReadOnlyList<DocumentGapItemDto>>> MissingDocumentsAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return Ok(await _completenessService.GetMissingDocumentsAsync(cancellationToken));
    }

    [HttpPost("claims/{claimId}/documents/completeness-check")]
    public async Task<ActionResult<ClaimCompletenessResult>> CheckCompletenessAsync(
        [FromRoute] string claimId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return Ok(await _completenessService.CheckClaimCompletenessAsync(claimId, cancellationToken));
    }
}

public sealed record CorrectFieldHttpRequest(string CorrectedValue, string? Reason);
public sealed record SetFieldStatusHttpRequest(string Status, string? Reason);
```

### 5.6 ChannelsController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Channels;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/channels")]
public sealed class ChannelsController : IipControllerBase
{
    private readonly IChannelQueryService _queryService;
    private readonly IPermissionService _permissionService;

    public ChannelsController(IChannelQueryService queryService, IPermissionService permissionService)
    {
        _queryService = queryService;
        _permissionService = permissionService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<IReadOnlyList<ChannelConversationRowDto>>> OverviewAsync(
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.ChannelsRead, cancellationToken);
        return Ok(await _queryService.GetOverviewAsync(cancellationToken));
    }
}
```

### 5.7 CustomersController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/customers")]
public sealed class CustomersController : IipControllerBase
{
    private readonly ICustomerQueryService _queryService;
    private readonly IPermissionService _permissionService;

    public CustomersController(ICustomerQueryService queryService, IPermissionService permissionService)
    {
        _queryService = queryService;
        _permissionService = permissionService;
    }

    [HttpGet("{customerId}")]
    public async Task<ActionResult<CustomerProfileDto?>> DetailAsync(
        [FromRoute] string customerId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.CustomersRead, cancellationToken);
        var result = await _queryService.GetCustomerProfileAsync(customerId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
```

### 5.8 SalesLeadsController

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.SalesLeads;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/sales-leads")]
public sealed class SalesLeadsController : IipControllerBase
{
    private readonly ISalesLeadQueryService _queryService;
    private readonly IPermissionService _permissionService;

    public SalesLeadsController(ISalesLeadQueryService queryService, IPermissionService permissionService)
    {
        _queryService = queryService;
        _permissionService = permissionService;
    }

    [HttpGet("{leadId}")]
    public async Task<ActionResult<SalesLeadDetailDto?>> DetailAsync(
        [FromRoute] string leadId,
        CancellationToken cancellationToken)
    {
        await _permissionService.EnsureAsync(Permissions.SalesLeadsRead, cancellationToken);
        var result = await _queryService.GetLeadDetailAsync(leadId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
```

### 5.9 AuditController

```csharp
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers;

[Route("api/audit")]
public sealed class AuditController : IipControllerBase
{
    private readonly IAuditQueryService _queryService;
    private readonly IPermissionService _permissionService;

    public AuditController(IAuditQueryService queryService, IPermissionService permissionService)
    {
        _queryService = queryService;
        _permissionService = permissionService;
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<AuditEventRowDto>>> EventsAsync(
        [FromQuery] string? eventType,
        [FromQuery] string? actorUserId,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await _permissionService.EnsureAsync(Permissions.AuditRead, cancellationToken);

        var result = await _queryService.ListEventsAsync(
            new AuditEventListQuery(eventType, actorUserId, targetType, targetId, fromUtc, toUtc, page, pageSize),
            cancellationToken);

        return Ok(result);
    }
}
```

---

## 6. Authentication and tenant-context middleware

### 6.1 Authentication modes and RuntimeProfile ownership

The API supports two auth modes from `InfrastructureOptions.AuthMode`:

```text
Entra       Real Microsoft Entra ID JWT bearer authentication.
Development Non-customer local development only. Forbidden in protected runtimes.
```

Protected runtimes:

```text
Staging
Production
ManagedApplicationReviewer
```

`RuntimeProfile`, `RuntimeProfileParser.Parse(string)`, and `RuntimeProfileExtensions.IsProtected()` are defined once in Document A. This document must consume those definitions and must not redefine a second runtime-profile type.

In protected runtimes, `AuthMode = Development` must fail startup through `AzureProviderStartupValidator.ThrowIfProductionInvalid` before any service is resolved.

### 6.2 Entra JWT registration

`InfrastructureOptions` uses flat Entra fields. Do not introduce a nested `options.Entra` object.

IIP's default identity model is a **single multitenant Microsoft Entra app registration** configured for **Accounts in any organizational directory**. The API must therefore validate tokens from organizational tenants through the `organizations` authority, not through a single insurer tenant issuer.

Field interpretation in the corrected model:

```text
EntraTenantId   IIP publisher/home Entra tenant where the single app registration is owned.
                This is not the insurer/customer tenant and must not be used to restrict JWT issuers.

EntraClientId   Client/application ID of IIP's own API/app registration. No model change needed.
EntraAudience   Audience/App ID URI for IIP's own API. No model change needed.
EntraIssuer     Legacy/single-tenant compatibility field only. Do not set TokenValidationParameters.ValidIssuer from this field in the multitenant model.
```

The preferred ASP.NET Core implementation uses `Microsoft.Identity.Web` because it has the multitenant-aware issuer-validation behavior required for non-single-tenant APIs. At implementation time, verify the exact overload and option names against the target `Microsoft.Identity.Web` package version. The architectural rule is fixed even if the exact overload changes: **do not hardcode one `ValidIssuer`; use the Microsoft identity middleware's multitenant issuer validation with `TenantId = organizations`.**

```csharp
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddIipAuthentication(
        this IServiceCollection services,
        InfrastructureOptions options,
        RuntimeProfile runtimeProfile)
    {
        if (runtimeProfile.IsProtected() && options.AuthMode == "Development")
        {
            throw new InvalidOperationException("Development authentication is forbidden in protected runtimes.");
        }

        if (options.AuthMode == "Entra")
        {
            if (string.IsNullOrWhiteSpace(options.EntraTenantId))
            {
                throw new InvalidOperationException(
                    "EntraTenantId is required when AuthMode is Entra. It must be IIP's publisher/home tenant, not an insurer tenant.");
            }

            if (string.IsNullOrWhiteSpace(options.EntraClientId))
            {
                throw new InvalidOperationException("EntraClientId is required when AuthMode is Entra.");
            }

            if (string.IsNullOrWhiteSpace(options.EntraAudience))
            {
                throw new InvalidOperationException("EntraAudience is required when AuthMode is Entra.");
            }

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    jwt =>
                    {
                        // Multitenant workforce mode for IIP's single app registration.
                        // Do not derive Authority from options.EntraTenantId; that value is IIP's publisher/home tenant.
                        jwt.Authority = "https://login.microsoftonline.com/organizations/v2.0";
                        jwt.Audience = options.EntraAudience;
                        jwt.RequireHttpsMetadata = true;

                        jwt.TokenValidationParameters ??= new TokenValidationParameters();
                        jwt.TokenValidationParameters.ValidateIssuer = true;
                        jwt.TokenValidationParameters.ValidateAudience = true;
                        jwt.TokenValidationParameters.ValidAudience = options.EntraAudience;
                        jwt.TokenValidationParameters.ValidateLifetime = true;
                        jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);

                        // Do not set ValidIssuer or ValidIssuers here. In non-single-tenant APIs,
                        // Microsoft.Identity.Web / IdentityModel must validate issuer dynamically
                        // against the organizational tenant metadata.
                    },
                    identity =>
                    {
                        identity.Instance = "https://login.microsoftonline.com/";
                        identity.TenantId = "organizations";
                        identity.ClientId = options.EntraClientId;
                        identity.Audience = options.EntraAudience;
                    });
        }
        else if (options.AuthMode == "Development")
        {
            if (runtimeProfile.IsProtected())
            {
                throw new InvalidOperationException("Development authentication is forbidden in protected runtimes.");
            }

            services
                .AddAuthentication(DevelopmentAuthenticationDefaults.Scheme)
                .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationDefaults.Scheme,
                    _ => { });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported authentication mode: {options.AuthMode}");
        }

        services.AddAuthorization();
        return services;
    }
}
```

`DevelopmentAuthenticationHandler` is permitted only for non-protected development or the isolated LocalTest host. `AzureProviderStartupValidator.ThrowIfProductionInvalid` must reject it in Staging, Production, and ManagedApplicationReviewer.

### 6.3 Tenant context population

Document A's tenant-context pipeline is authoritative and must be used unchanged:

```text
TenantWorkspaceContextMiddleware
IAuthenticatedTenantResolver
AuthenticatedTenantResolver
TenantContextAccessor
ITenantContextAccessor
ITenantContextSetter
IPermissionReadRepository
PermissionService
```

This document must not define or register a parallel tenant-context middleware, writer, scoped accessor, or third permission-resolver abstraction. Permission resolution must use Document A's `IPermissionReadRepository` and `PermissionService`.

The request pipeline must work as follows:

```text
Authenticated principal
-> TenantWorkspaceContextMiddleware
-> AuthenticatedTenantResolver
-> IPermissionReadRepository.GetEffectiveMembershipAsync(...)
-> validated TenantContext set through ITenantContextSetter
-> controller calls Application service
```

The Entra `tid` claim is only a hint used during membership lookup. It is **not** the IIP application tenant authority. The final `TenantContext.TenantId`, `WorkspaceId`, `Roles`, and `Permissions` must come from the validated membership record returned by `IPermissionReadRepository.GetEffectiveMembershipAsync(...)`.

#### 6.3.1 External identity key strategy

`AuthenticatedTenantResolver` must preserve the existing server-side membership check but tighten the external user identifier strategy used before calling `IPermissionReadRepository.GetEffectiveMembershipAsync(...)`.

Preferred key order:

```text
1. If both oid and tid exist: externalUserId = $"entra:{tid}:{oid}".
2. Else if oid exists without tid: externalUserId = $"entra:unknown-tenant:{oid}" and audit the degraded identity shape.
3. Else if sub exists: externalUserId = $"entra-sub:{sub}" as a last-resort compatibility fallback only.
4. Never use email, preferred_username, unique_name, or upn for authorization or membership keys.
```

Implementation shape:

```csharp
private static string ResolveExternalUserId(ClaimsPrincipal principal)
{
    var oid = principal.FindFirstValue("oid");
    var tid = principal.FindFirstValue("tid");
    var sub = principal.FindFirstValue("sub");

    if (!string.IsNullOrWhiteSpace(oid) && !string.IsNullOrWhiteSpace(tid))
    {
        return $"entra:{tid}:{oid}";
    }

    if (!string.IsNullOrWhiteSpace(oid))
    {
        return $"entra:unknown-tenant:{oid}";
    }

    if (!string.IsNullOrWhiteSpace(sub))
    {
        return $"entra-sub:{sub}";
    }

    throw new UnauthorizedAccessException("Authenticated principal does not contain a usable immutable subject identifier.");
}
```

The `tid` value in the external identity key is still not business authority. It is an identity namespace used to find the matching IIP membership record. Tenant, workspace, role, and permission authority still comes only from the validated membership returned by `IPermissionReadRepository`.

### 6.4 Middleware ordering

The production API must register Document A's API tenant context services:

```csharp
builder.Services.AddApplicationCorePorts();
builder.Services.AddApiTenantContext();
```

The runtime pipeline must use Document A's middleware extension:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseIipTenantContext();
```

`UseIipTenantContext()` must run before controller actions execute. Controllers and Application services must never read tenant/workspace/permission authority directly from route parameters, query strings, request bodies, arbitrary client headers, or ad hoc `IHttpContextAccessor` calls.

## 7. Global error handling

A single middleware maps exceptions consistently.

### 7.1 Mapping

| Exception | Status | Code |
|---|---:|---|
| `UnauthorizedAccessException` | `401` | `unauthorized` |
| `ForbiddenException` | `403` | `forbidden` |
| `ValidationException` | `400` | `validation_failed` |
| `NotFoundOrNotVisibleException` | `404` | `not_found_or_not_visible` |
| `ProviderNotConfiguredException` | `503` | `not_configured` |
| `ProviderUnavailableException` | `503` | `provider_unavailable` |
| `WorkflowConflictException` | `409` | `workflow_conflict` |
| Any other exception | `500` | `internal_error` |

### 7.2 Middleware implementation

```csharp
namespace InsuranceIntelligence.Api.Middleware;

public sealed class SafeExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SafeExceptionHandlingMiddleware> _logger;

    public SafeExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<SafeExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = correlationIdAccessor.Current;
            var (status, code, message) = MapException(ex);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled API error. CorrelationId: {CorrelationId}", correlationId);
            }
            else
            {
                _logger.LogWarning(ex, "Handled API error {Code}. CorrelationId: {CorrelationId}", code, correlationId);
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";

            var body = new ApiErrorResponse(new ApiError(
                Code: code,
                Message: message,
                CorrelationId: correlationId));

            await context.Response.WriteAsJsonAsync(body);
        }
    }

    private static (int Status, string Code, string Message) MapException(Exception ex) => ex switch
    {
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Authentication is required."),
        ForbiddenException => (StatusCodes.Status403Forbidden, "forbidden", "You do not have permission to perform this action."),
        ValidationException validation => (StatusCodes.Status400BadRequest, "validation_failed", validation.Message),
        NotFoundOrNotVisibleException => (StatusCodes.Status404NotFound, "not_found_or_not_visible", "The requested record was not found or is not visible."),
        ProviderNotConfiguredException provider => (StatusCodes.Status503ServiceUnavailable, "not_configured", SafeProviderMessage(provider.Message)),
        ProviderUnavailableException provider => (StatusCodes.Status503ServiceUnavailable, "provider_unavailable", SafeProviderMessage(provider.Message)),
        WorkflowConflictException conflict => (StatusCodes.Status409Conflict, "workflow_conflict", conflict.Message),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred. Use the correlation ID when contacting support.")
    };

    private static string SafeProviderMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "A required provider is not configured or unavailable."
            : message;
    }
}
```

No controller should implement its own try/catch for these mappings.

---

## 8. Composition root: Program.cs

### 8.1 Registration order

The composition root must register and validate in this order:

```text
1. Create builder.
2. Bind InfrastructureOptions from configuration.
3. Resolve runtime profile string through AzureProviderStartupValidator.ResolveRuntimeProfile(...).
4. Parse the runtime profile string with RuntimeProfileParser.Parse(...).
5. Run AzureProviderStartupValidator.Validate(options).
6. Run AzureProviderStartupValidator.ThrowIfProductionInvalid(options, environmentName).
7. Register logging, telemetry, and correlation ID.
8. Register authentication and authorization.
9. Register Document A's Application core ports and API tenant-context resolver/middleware services.
10. Register EF Core persistence and real Infrastructure providers.
11. Register Document A's Infrastructure implementations of IPermissionReadRepository, IAuditWriter, and IProviderHealthCheck.
12. Register the twelve Application services.
13. Register controllers.
14. Register OpenAPI.
15. Build app.
16. Apply middleware order.
17. Map controllers.
```

### 8.2 Production API Program.cs

```csharp
using InsuranceIntelligence.Api.DependencyInjection;
using InsuranceIntelligence.Api.Middleware;
using InsuranceIntelligence.Application.Approvals;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Common;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

var infrastructureOptions = builder.Configuration
    .GetSection("Infrastructure")
    .Get<InfrastructureOptions>()
    ?? throw new InvalidOperationException("Infrastructure options are missing.");

var runtimeProfile = RuntimeProfileParser.Parse(
    AzureProviderStartupValidator.ResolveRuntimeProfile(
        infrastructureOptions,
        builder.Environment.EnvironmentName));

AzureProviderStartupValidator.Validate(infrastructureOptions);
AzureProviderStartupValidator.ThrowIfProductionInvalid(
    infrastructureOptions,
    builder.Environment.EnvironmentName);

if (runtimeProfile == RuntimeProfile.LocalTest)
{
    throw new InvalidOperationException(
        "The production API host cannot run LocalTest profile. Use InsuranceIntelligence.Api.LocalTestHost instead.");
}

builder.Services.AddSingleton(infrastructureOptions);
builder.Services.AddSingleton(runtimeProfile);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddLogging();
builder.Services.AddCorrelationIdServices();

builder.Services.AddIipAuthentication(infrastructureOptions, runtimeProfile);

// Document A core ports and tenant-context pipeline.
builder.Services.AddApplicationCorePorts();
builder.Services.AddApiTenantContext();

// Real Infrastructure only. These extension methods must reject unsafe provider modes in protected runtimes.
builder.Services.AddInfrastructureProviders(infrastructureOptions, runtimeProfile);
builder.Services.AddEfRepositories(infrastructureOptions);
builder.Services.AddCorePortInfrastructure(infrastructureOptions, builder.Environment.EnvironmentName);

// Twelve fixed Application-layer services.
builder.Services.AddScoped<ICommandCenterQueryService, CommandCenterQueryService>();
builder.Services.AddScoped<IClaimsQueryService, ClaimsQueryService>();
builder.Services.AddScoped<ISlaRiskService, SlaRiskService>();
builder.Services.AddScoped<IApprovalQueryService, ApprovalQueryService>();
builder.Services.AddScoped<IApprovalDecisionService, ApprovalDecisionService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IDocumentExtractionWorkflowService, DocumentExtractionWorkflowService>();
builder.Services.AddScoped<IDocumentReviewService, DocumentReviewService>();
builder.Services.AddScoped<ICompletenessCheckService, CompletenessCheckService>();
builder.Services.AddScoped<IChannelQueryService, ChannelQueryService>();
builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();
builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SafeExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseIipTenantContext();

if (!runtimeProfile.IsProtected())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "internal/openapi/{documentName}.json";
    });
}

app.MapControllers();
app.Run();
```

Program.cs must not register any parallel tenant-context middleware, writer, scoped accessor, or third permission-resolver abstraction. Document A already supplies the authoritative tenant and permission pipeline through `IPermissionReadRepository`, `PermissionService`, `TenantWorkspaceContextMiddleware`, and `AuthenticatedTenantResolver`.

### 8.3 RuntimeProfile helper ownership

`RuntimeProfile`, `RuntimeProfileParser.Parse(string)`, and `RuntimeProfileExtensions.IsProtected()` are defined in Document A and imported here. Document B must not define a duplicate enum or duplicate extension method.

Usage pattern:

```csharp
var runtimeProfile = RuntimeProfileParser.Parse(
    AzureProviderStartupValidator.ResolveRuntimeProfile(options, environmentName));

if (runtimeProfile.IsProtected())
{
    // protected-runtime checks
}
```

### 8.4 LocalTest host composition root

LocalTest implementations must be physically isolated in `InsuranceIntelligence.LocalTestDoubles`. They must not be referenced by `InsuranceIntelligence.Api`.

```csharp
// Project: src/InsuranceIntelligence.Api.LocalTestHost/Program.cs
// This project references InsuranceIntelligence.LocalTestDoubles.
// The real InsuranceIntelligence.Api project must never reference InsuranceIntelligence.LocalTestDoubles.

using InsuranceIntelligence.LocalTestDoubles.Extensions;

var builder = WebApplication.CreateBuilder(args);

var infrastructureOptions = builder.Configuration
    .GetSection("Infrastructure")
    .Get<InfrastructureOptions>()
    ?? throw new InvalidOperationException("Infrastructure options are missing.");

var runtimeProfile = RuntimeProfileParser.Parse(
    AzureProviderStartupValidator.ResolveRuntimeProfile(
        infrastructureOptions,
        builder.Environment.EnvironmentName));

if (runtimeProfile != RuntimeProfile.LocalTest)
{
    throw new InvalidOperationException(
        "InsuranceIntelligence.Api.LocalTestHost can only run with RuntimeProfile = LocalTest.");
}

builder.Services.AddControllers()
    .AddApplicationPart(typeof(CommandCenterController).Assembly);

builder.Services.AddLocalTestPortsGuarded(runtimeProfile);
builder.Services.AddApplicationServicesForLocalTest();

var app = builder.Build();
app.MapControllers();
app.Run();
```

The production API host refuses LocalTest. The LocalTest host refuses non-LocalTest. This creates both a compile-time project-reference boundary and a startup-time guard.

### 8.5 LocalTest guarded registration

```csharp
// Project: InsuranceIntelligence.LocalTestDoubles

using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.LocalTestDoubles.Extensions;

public static class LocalTestRegistration
{
    public static IServiceCollection AddLocalTestPortsGuarded(
        this IServiceCollection services,
        RuntimeProfile runtimeProfile)
    {
        if (runtimeProfile != RuntimeProfile.LocalTest)
        {
            throw new InvalidOperationException("LocalTest ports cannot be registered outside LocalTest.");
        }

        services.AddScoped<ITenantContextAccessor, LocalTestTenantContextAccessor>();
        services.AddScoped<IPermissionService, LocalTestPermissionService>();
        services.AddScoped<IAuditWriter, InMemoryAuditWriter>();
        services.AddScoped<IProviderHealthCheck, LocalTestProviderHealthCheck>();

        return services;
    }
}
```

These types must never appear in the production API project file, production DI registration, or production publish output.

## 9. Infrastructure registration hooks

The Infrastructure layer owns real provider registrations. The API composition root calls these extension methods after `AzureProviderStartupValidator.Validate(...)` and `AzureProviderStartupValidator.ThrowIfProductionInvalid(...)` have already run.

```csharp
namespace InsuranceIntelligence.Infrastructure.Extensions;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructureProviders(
        this IServiceCollection services,
        InfrastructureOptions options,
        RuntimeProfile runtimeProfile)
    {
        if (runtimeProfile.IsProtected())
        {
            RejectLocalOrMockProviderModes(options);
        }

        services.AddAzureBlobProvider(options.AzureBlob);
        services.AddAzureDocumentIntelligenceProvider(options.DocumentIntelligence);
        services.AddAzureAiSearchProvider(options.AzureAiSearch);
        services.AddAzureOpenAiProvider(options.AzureOpenAI);
        services.AddAzureServiceBusProvider(options.ServiceBus);
        services.AddGraphMailProvider(options.GraphMail);

        return services;
    }

    public static IServiceCollection AddEfRepositories(
        this IServiceCollection services,
        InfrastructureOptions options)
    {
        if (!string.Equals(options.StorageProvider, "SqlServer", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only SqlServer storage is allowed for the deployable API host.");
        }

        if (string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            throw new InvalidOperationException("SqlServerConnectionString is required when StorageProvider is SqlServer.");
        }

        services.AddDbContext<IipDbContext>(db =>
        {
            db.UseSqlServer(options.SqlServerConnectionString);
        });

        // Core repositories consumed by Document A's core ports.
        services.AddScoped<IPermissionReadRepository, EfPermissionReadRepository>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();

        // Repositories consumed by the twelve Application services.
        services.AddScoped<IClaimsReadRepository, EfClaimsReadRepository>();
        services.AddScoped<IClaimsSlaReadRepository, EfClaimsSlaReadRepository>();
        services.AddScoped<IApprovalReadRepository, EfApprovalReadRepository>();
        services.AddScoped<IApprovalDecisionRepository, EfApprovalDecisionRepository>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IDocumentExtractionRepository, EfDocumentExtractionRepository>();
        services.AddScoped<IDocumentReviewRepository, EfDocumentReviewRepository>();
        services.AddScoped<ICompletenessRuleRepository, EfCompletenessRuleRepository>();
        services.AddScoped<IClaimDocumentReadRepository, EfClaimDocumentReadRepository>();
        services.AddScoped<IChannelReadRepository, EfChannelReadRepository>();
        services.AddScoped<ICustomerReadRepository, EfCustomerReadRepository>();
        services.AddScoped<ISalesLeadReadRepository, EfSalesLeadReadRepository>();
        services.AddScoped<IAuditReadRepository, EfAuditReadRepository>();

        return services;
    }

    private static void RejectLocalOrMockProviderModes(InfrastructureOptions options)
    {
        var forbidden = new[]
        {
            "Mock",
            "Simulated",
            "Placeholder",
            "InMemory",
            "SQLite",
            "DevelopmentAuth",
            "Demo",
            "SeededDemoBusinessData",
            "FallbackToMock"
        };

        var selectedModes = new[]
        {
            options.StorageProvider,
            options.BlobProvider,
            options.DocumentProvider,
            options.RetrievalProvider,
            options.AiProvider,
            options.QueueProvider,
            options.EmailProvider,
            options.CommunicationProvider,
            options.NotificationProvider,
            options.AuthMode
        };

        if (selectedModes.Any(mode => forbidden.Contains(mode, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Protected runtime cannot use mock, local, development, or simulated provider modes.");
        }
    }
}
```

`AzureProviderStartupValidator.ThrowIfProductionInvalid` remains the primary startup gate. `RejectLocalOrMockProviderModes` is defense in depth at DI registration time. Do not introduce a third permission-resolver abstraction; role and permission resolution belongs to `IPermissionReadRepository` and `PermissionService`.

## 10. OpenAPI / Swagger setup

OpenAPI generation is required.

Rules:

```text
- Generate OpenAPI JSON for internal tooling and contract validation.
- Expose Swagger UI only in non-protected runtime profiles.
- In Staging/Production/ManagedApplicationReviewer, do not expose Swagger UI publicly.
- In protected profiles, the JSON endpoint may be enabled only behind internal network/auth controls.
```

Minimal setup:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Insurance Intelligence Platform API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Microsoft Entra ID JWT bearer token."
    });
});
```

Swagger UI gating:

```csharp
if (!runtimeProfile.IsProtected())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "internal/openapi/{documentName}.json";
    });
}
```

---

## 11. Tests to add

### 11.1 No bearer token rejected before service invocation

```csharp
[Fact]
public async Task RequestWithoutBearerToken_IsRejectedBeforeServiceInvocation()
{
    await using var factory = new ApiWebApplicationFactory(profile: RuntimeProfile.Staging);
    var client = factory.CreateClient();

    var response = await client.GetAsync("/api/claims");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    factory.GetMock<IClaimsQueryService>()
        .Verify(x => x.ListClaimsAsync(It.IsAny<ClaimsListQuery>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

### 11.2 Valid token missing permission returns 403 before repository invocation

```csharp
[Fact]
public async Task ValidTokenMissingPermission_Returns403BeforeRepositoryInvocation()
{
    await using var factory = new ApiWebApplicationFactory(profile: RuntimeProfile.LocalTest)
        .WithAuthenticatedUser(
            tenantId: "tenant-a",
            workspaceId: "workspace-a",
            userId: "user-no-claims-read",
            permissions: Array.Empty<string>());

    var client = factory.CreateClient();

    var response = await client.GetAsync("/api/claims");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    factory.GetMock<IClaimsReadRepository>()
        .Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ClaimsListQuery>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

### 11.3 End-to-end tenant mismatch cannot read another tenant

```csharp
[Fact]
public async Task ClaimsDetail_DoesNotReturnCrossTenantData_EndToEnd()
{
    await using var factory = new ApiWebApplicationFactory(profile: RuntimeProfile.LocalTest)
        .WithAuthenticatedUser(
            tenantId: "tenant-a",
            workspaceId: "workspace-a",
            userId: "user-1",
            permissions: new[] { Permissions.ClaimsRead })
        .SeedClaim(
            tenantId: "tenant-b",
            workspaceId: "workspace-b",
            claimId: "clm-cross-tenant");

    var client = factory.CreateClientWithBearerToken();

    var response = await client.GetAsync("/api/claims/clm-cross-tenant");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

This test must go through the real ASP.NET Core pipeline. Do not replace it with only a service unit test.

### 11.4 ManagedApplicationReviewer startup fails closed with incomplete provider configuration

```csharp
[Fact]
public void ManagedApplicationReviewer_WithIncompleteProviderConfiguration_FailsStartup()
{
    var options = new InfrastructureOptions
    {
        RuntimeProfile = "ManagedApplicationReviewer",
        AuthMode = "Entra",
        StorageProvider = "SqlServer",
        BlobProvider = "AzureBlob",
        DocumentProvider = "AzureDocumentIntelligence",
        RetrievalProvider = "AzureAiSearch",
        AiProvider = "AzureOpenAI",
        QueueProvider = "AzureServiceBus",
        EmailProvider = "Graph",
        SeedDemoData = false,
        AllowProductionMockProviders = false,
        AzureBlob = new AzureBlobOptions { AccountName = null }
    };

    var ex = Assert.Throws<InvalidOperationException>(() =>
        AzureProviderStartupValidator.ThrowIfProductionInvalid(options, "ManagedApplicationReviewer"));

    Assert.Contains("Azure Blob", ex.Message, StringComparison.OrdinalIgnoreCase);
}
```

### 11.5 LocalTest implementation cannot resolve outside LocalTest

```csharp
[Theory]
[InlineData(RuntimeProfile.Staging)]
[InlineData(RuntimeProfile.Production)]
[InlineData(RuntimeProfile.ManagedApplicationReviewer)]
public void ProtectedRuntime_CannotResolveLocalTestPorts(RuntimeProfile profile)
{
    var services = new ServiceCollection();

    Assert.Throws<InvalidOperationException>(() =>
        services.AddLocalTestPortsGuarded(profile));
}
```

### 11.6 Production API assembly does not reference LocalTest assembly

```csharp
[Fact]
public void ProductionApiProject_DoesNotReferenceLocalTestAssembly()
{
    var apiAssembly = typeof(CommandCenterController).Assembly;
    var referenced = apiAssembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);

    Assert.DoesNotContain("InsuranceIntelligence.LocalTestDoubles", referenced);
}
```

### 11.7 Global error middleware includes correlation ID and no stack trace

```csharp
[Fact]
public async Task GlobalExceptionMiddleware_ReturnsCorrelationIdWithoutStackTrace()
{
    await using var factory = new ApiWebApplicationFactory(profile: RuntimeProfile.LocalTest)
        .WithAuthenticatedUser(
            tenantId: "tenant-a",
            workspaceId: "workspace-a",
            userId: "user-1",
            permissions: new[] { Permissions.ClaimsRead })
        .WithServiceFailure<IClaimsQueryService>(new InvalidOperationException("Sensitive internal failure details."));

    var response = await factory.CreateClientWithBearerToken().GetAsync("/api/claims");
    var body = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Contains("correlationId", body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("at InsuranceIntelligence", body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Sensitive internal failure details", body, StringComparison.OrdinalIgnoreCase);
}
```

---

## 12. Manual Azure / Entra actions required

Before protected-runtime execution:

```text
1. Create or confirm the single multitenant Microsoft Entra app registration for IIP API, configured for Accounts in any organizational directory.
2. Configure valid API audience/application ID URI for the one IIP API registration.
3. Configure accessTokenAcceptedVersion for the API to issue/accept the intended token version for the multitenant app.
4. Configure JWT validation in the API for organizations authority mode, not a single insurer tenant issuer.
5. Treat EntraTenantId as IIP's publisher/home tenant where the app registration is owned; do not set it to the insurer/customer tenant.
6. Complete publisher verification before broad insurer rollout.
7. Configure tenant onboarding/admin-consent flow for each insurer tenant.
8. Optionally define coarse app roles such as IIP.Access, IIP.Admin, and IIP.ReadOnly for insurer-side entry gating; do not map raw group claims directly to fine-grained IIP permissions.
9. Configure IIP's own role/permission store through IPermissionReadRepository records for fine-grained permissions.
10. Configure Azure SQL connection through managed identity or secure connection settings.
11. Configure Azure Blob Storage, Azure AI Document Intelligence, Azure AI Search, Azure OpenAI, Azure Service Bus, and Microsoft Graph options.
12. Confirm ProviderHealthOptions.TimeoutSeconds is set to a safe low timeout.
13. Confirm Application Insights / Log Analytics connection.
14. Confirm Swagger UI is disabled in protected runtimes.
15. Confirm LocalTest assemblies are absent from protected publish output.
```

---

## 13. Completion report template

```text
Document produced: IIP Backend Wiring — Controllers, Composition Root, and Request Pipeline.

Files to create or modify:
- src/InsuranceIntelligence.Api/Controllers/IipControllerBase.cs
- src/InsuranceIntelligence.Api/Controllers/CommandCenterController.cs
- src/InsuranceIntelligence.Api/Controllers/ClaimsController.cs
- src/InsuranceIntelligence.Api/Controllers/ApprovalsController.cs
- src/InsuranceIntelligence.Api/Controllers/DocumentsController.cs
- src/InsuranceIntelligence.Api/Controllers/ChannelsController.cs
- src/InsuranceIntelligence.Api/Controllers/CustomersController.cs
- src/InsuranceIntelligence.Api/Controllers/SalesLeadsController.cs
- src/InsuranceIntelligence.Api/Controllers/AuditController.cs
- src/InsuranceIntelligence.Api/Middleware/CorrelationIdMiddleware.cs
- - src/InsuranceIntelligence.Api/Middleware/SafeExceptionHandlingMiddleware.cs
- src/InsuranceIntelligence.Api/Extensions/AuthenticationRegistration.cs
- docs/identity/IIP_Multi_Tenant_Identity_Onboarding_App_Roles_and_SCIM_Provisioning.md
- src/InsuranceIntelligence.Api/Program.cs
- src/InsuranceIntelligence.Api.LocalTestHost/Program.cs
- src/InsuranceIntelligence.LocalTestDoubles/Extensions/LocalTestRegistration.cs
- src/InsuranceIntelligence.Tests/Api/AuthenticationPipelineTests.cs
- src/InsuranceIntelligence.Tests/Api/AuthorizationPipelineTests.cs
- src/InsuranceIntelligence.Tests/Api/TenantIsolationPipelineTests.cs
- src/InsuranceIntelligence.Tests/Api/StartupValidationTests.cs
- src/InsuranceIntelligence.Tests/Api/LocalTestIsolationTests.cs

Routes added:
- POST /api/command-center/query
- GET /api/claims
- GET /api/claims/{claimId}
- GET /api/claims/attention
- GET /api/claims/daily-summary
- GET /api/claims/{claimId}/draft-context
- GET /api/claims/sla-risks
- GET /api/claims/{claimId}/sla-status
- GET /api/approvals/queue
- POST /api/approvals/{approvalId}/decisions
- POST /api/documents/uploads
- POST /api/documents/{documentId}/extraction
- GET /api/documents/{documentId}/fields
- PATCH /api/documents/{documentId}/fields/{fieldId}/correction
- PATCH /api/documents/{documentId}/fields/{fieldId}/status
- GET /api/documents/missing
- POST /api/claims/{claimId}/documents/completeness-check
- GET /api/channels/overview
- GET /api/customers/{customerId}
- GET /api/sales-leads/{leadId}
- GET /api/audit/events

Security impact:
- Adds Entra JWT bearer authentication for protected runtimes.
- Adds server-side permission checks at controller and service layers.
- Adds centralized safe error mapping.
- Prevents Swagger UI exposure in protected profiles.
- Prevents AI-only final decision routes.

Tenant isolation impact:
- Tenant/workspace/user context is populated server-side from authenticated principal.
- Controllers do not accept tenant/workspace as authority.
- Service and repository layers still enforce tenant/workspace scope.

Authentication/authorization impact:
- AuthMode=Development is rejected in protected runtimes.
- JWT bearer validation is corrected to the single multitenant IIP Entra app model using organizations authority mode.
- EntraTenantId is explicitly treated as IIP's publisher/home tenant, not as an insurer tenant issuer restriction.
- EntraClientId and EntraAudience remain the single IIP app/API identifiers and do not change between insurer tenants.
- ValidIssuer/options.EntraIssuer is no longer used as a hardcoded single-tenant issuer in the multitenant model.
- AuthenticatedTenantResolver continues to treat tid as an identity hint only and validates real tenant/workspace membership through IPermissionReadRepository.
- External user identity keys prefer tid+oid, with sub only as a last-resort fallback.
- Missing bearer token is rejected before service invocation.
- Missing permission returns 403 before repository invocation.

Tests to add/run:
- RequestWithoutBearerToken_IsRejectedBeforeServiceInvocation
- ValidTokenMissingPermission_Returns403BeforeRepositoryInvocation
- ClaimsDetail_DoesNotReturnCrossTenantData_EndToEnd
- ManagedApplicationReviewer_WithIncompleteProviderConfiguration_FailsStartup
- ProtectedRuntime_CannotResolveLocalTestPorts
- ProductionApiProject_DoesNotReferenceLocalTestAssembly
- GlobalExceptionMiddleware_ReturnsCorrelationIdWithoutStackTrace

Manual Azure/Entra actions required:
- Configure one multitenant IIP Entra API registration for Accounts in any organizational directory.
- Configure organizations authority mode in the API, not a fixed insurer tenant issuer.
- Configure publisher verification before broad insurer rollout.
- Build the insurer admin-consent onboarding flow and bootstrap internal IIP membership after consent.
- Configure optional coarse app roles only as entry gates; keep fine-grained permissions in IIP's role/permission store.
- Configure real Azure providers and managed identity access.
- Configure Application Insights / Log Analytics.
- Confirm protected-runtime provider health and startup gates.

Remaining blockers:
- Confirm exact .NET 8 / Microsoft.Identity.Web multitenant configuration syntax for the target package version before implementation.
- Confirm final route versioning policy: this pass standardizes `/api`; use one approved versioning change if `/api/v1` is required.
- Confirm whether insurer enterprise-app assignment is required from day one or optional per tenant.
- Confirm role-to-permission store schema used by `IPermissionReadRepository`.
- Confirm OpenAPI JSON exposure rules for internal tooling in protected profiles.

No-fake-data confirmation:
- No mock, in-memory, simulated, placeholder, seeded-demo, or fake operational data path is introduced by this wiring.
- LocalTest doubles remain physically isolated in `InsuranceIntelligence.LocalTestDoubles`; only `InsuranceIntelligence.Api.LocalTestHost` may reference that assembly.

Recommended next step:
- Implement `InsuranceIntelligence.Api` controllers and middleware first, then add protected-runtime startup tests before connecting frontend calls.
```
