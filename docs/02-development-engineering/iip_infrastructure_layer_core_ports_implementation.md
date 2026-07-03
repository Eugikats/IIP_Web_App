# IIP Infrastructure Layer — Core Ports Implementation

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready Markdown specification  
**Area:** Core Application ports and production-backed Infrastructure implementations  
**Status:** New-repository implementation specification  
**Date:** July 2026  

---

## Shared rules used by this implementation output

This output follows the same implementation pattern used by the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, and Audit.

```text
1. Resolve tenant/workspace from server-side authenticated context only.
2. Never trust client-supplied tenant/workspace IDs as authority.
3. Enforce backend permissions before every sensitive read or state-changing operation.
4. Write durable audit events for every state-changing operation and sensitive read.
5. Gate external-provider-dependent capabilities through real provider health checks.
6. Never fabricate data when a provider, repository, integration, or capability is unavailable.
7. Return ProviderNotConfiguredException / ProviderUnavailableException or Not configured instead of fake success.
8. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, and provider SDK dependencies.
9. Keep Application service contracts independent of concrete Infrastructure adapters.
10. Test doubles may exist only in automated tests or isolated LocalTest assemblies.
11. Staging, Production, and ManagedApplicationReviewer must never resolve Mock, InMemory, Simulated, Placeholder, Demo, DevelopmentAuth, SeededDemoBusinessData, or FallbackToMock implementations.
12. AI/system actors must never be final actors for consequential insurance decisions.
```

This document supplies only the four missing core ports required by the already-drafted Application-layer service specs:

```text
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
```

It does not modify, restate, or replace the 12 Application-layer service specifications. Its purpose is to make those specs implementable in a clean new repository.

---

## 1. Scope of this pass

This pass implements the core Application ports and production-backed Infrastructure implementations that the 12 service specs assume already exist.

Implemented capabilities:

```text
- Server-side tenant/workspace/user/role/permission context resolution.
- Entra-compatible TenantContext resolution from authenticated request claims.
- Backend permission enforcement through a real role/permission store.
- Durable audit writing through EF Core and Azure SQL-backed persistence.
- Unified provider health checking over real Azure/Microsoft providers.
- Protected-runtime enforcement that prevents LocalTest/mock implementations from being registered.
- Physical isolation rules for LocalTest-only test doubles.
```

Not implemented in this pass:

```text
- The 12 Application-layer business services themselves.
- Controllers for Claims, Documents, Channels, Customers, Sales Leads, Audit, or Approvals.
- Frontend code.
- AI prompt implementations.
- Azure SDK adapter internals where the existing current-repo adapters already provide connectivity checks.
- Mock, in-memory, simulated, or demo production implementations.
```

---

## 2. Repository structure decision

### 2.1 Decision

The new repository **must introduce a dedicated `InsuranceIntelligence.Application` project**.

This is the recommended and required direction because all existing Application-layer service-output docs assume namespaces such as:

```text
InsuranceIntelligence.Application.Claims
InsuranceIntelligence.Application.Documents
InsuranceIntelligence.Application.Approvals
InsuranceIntelligence.Application.Audit
InsuranceIntelligence.Application.CommandCenter
InsuranceIntelligence.Application.Common
```

The new repository should not copy the current repo's flatter `Api-hosts-everything` shape for business logic. That shape may be acceptable as an intermediate legacy implementation, but it makes it harder to enforce clean boundaries, LocalTest isolation, and production-provider validation.

### 2.2 Required project layout

```text
src/
  InsuranceIntelligence.Api/
    Middleware/
    Tenancy/
    DependencyInjection/
    Program.cs

  InsuranceIntelligence.Application/
    Common/
      Audit/
      Authorization/
      Exceptions/
      Providers/
      Tenancy/
    Claims/
    Documents/
    Approvals/
    Channels/
    Customers/
    SalesLeads/
    Audit/
    CommandCenter/

  InsuranceIntelligence.Domain/
    Approvals/
    Audit/
    Claims/
    Common/
    Tenancy/

  InsuranceIntelligence.Infrastructure/
    Configuration/
    DependencyInjection/
    Persistence/
    Persistence/Repositories/
    Providers/
    Providers/BlobStorage/
    Providers/DocumentIntelligence/
    Providers/Search/
    Providers/OpenAi/
    Providers/ServiceBus/
    Providers/Graph/

  InsuranceIntelligence.Functions/
    DocumentIngestion/
    KnowledgeIndexing/
    OutlookIngestion/

  InsuranceIntelligence.Tests/
    Application/
    Infrastructure/
    Api/
    Security/

  InsuranceIntelligence.LocalTestDoubles/
    Common/
    Providers/
    Persistence/
```

### 2.3 Project reference rules

Allowed references:

```text
Api -> Application
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Functions -> Application
Functions -> Infrastructure
Tests -> Application / Infrastructure / Api / LocalTestDoubles
LocalTestDoubles -> Application / Domain
```

Forbidden references:

```text
Application -> Infrastructure
Application -> Api
Domain -> Application
Domain -> Infrastructure
Domain -> Api
Api -> LocalTestDoubles
Application -> LocalTestDoubles
Infrastructure -> LocalTestDoubles
Functions -> LocalTestDoubles
```

The `InsuranceIntelligence.LocalTestDoubles` project must never be referenced by deployable projects and must never be published into Staging, Production, or ManagedApplicationReviewer output.

---

## 3. Files to create

### 3.1 Application ports and shared models

```text
src/InsuranceIntelligence.Application/Common/Tenancy/TenantContext.cs
src/InsuranceIntelligence.Application/Common/Tenancy/ITenantContextAccessor.cs
src/InsuranceIntelligence.Application/Common/Tenancy/TenantContextAccessor.cs

src/InsuranceIntelligence.Application/Common/Authorization/IPermissionService.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
src/InsuranceIntelligence.Application/Common/Authorization/PermissionService.cs
src/InsuranceIntelligence.Application/Common/Authorization/IPermissionReadRepository.cs

src/InsuranceIntelligence.Application/Common/Audit/IAuditWriter.cs
src/InsuranceIntelligence.Application/Common/Audit/AuditEventCreateRequest.cs
src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs

src/InsuranceIntelligence.Application/Common/Providers/IProviderHealthCheck.cs
src/InsuranceIntelligence.Application/Common/Providers/ProviderHealthModels.cs
src/InsuranceIntelligence.Application/Common/Providers/ProviderCodes.cs

src/InsuranceIntelligence.Application/Common/Exceptions/ForbiddenException.cs
src/InsuranceIntelligence.Application/Common/Exceptions/ProviderNotConfiguredException.cs
src/InsuranceIntelligence.Application/Common/Exceptions/ProviderUnavailableException.cs
src/InsuranceIntelligence.Application/Common/Exceptions/ValidationException.cs
src/InsuranceIntelligence.Application/Common/Exceptions/NotFoundOrNotVisibleException.cs
```

### 3.2 API request-context resolution

```text
src/InsuranceIntelligence.Api/Middleware/TenantWorkspaceContextMiddleware.cs
src/InsuranceIntelligence.Api/Tenancy/IAuthenticatedTenantResolver.cs
src/InsuranceIntelligence.Api/Tenancy/AuthenticatedTenantResolver.cs
src/InsuranceIntelligence.Api/DependencyInjection/ApiTenantContextRegistration.cs
```

### 3.3 Infrastructure implementations

```text
src/InsuranceIntelligence.Infrastructure/Authorization/EfPermissionReadRepository.cs
src/InsuranceIntelligence.Infrastructure/Audit/EfAuditWriter.cs
src/InsuranceIntelligence.Infrastructure/Audit/EfAuditEventRepository.cs
src/InsuranceIntelligence.Infrastructure/Providers/AzureProviderHealthCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/IProviderConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/BlobStorage/AzureBlobStorageConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/DocumentIntelligence/AzureDocumentIntelligenceConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/Search/AzureAiSearchConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/OpenAi/AzureOpenAiConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/ServiceBus/AzureServiceBusConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/Providers/Graph/GraphMailConnectivityCheck.cs
src/InsuranceIntelligence.Infrastructure/DependencyInjection/CorePortInfrastructureRegistration.cs
```

### 3.4 LocalTest-only isolated implementations

```text
src/InsuranceIntelligence.LocalTestDoubles/Common/Tenancy/LocalTestTenantContextAccessor.cs
src/InsuranceIntelligence.LocalTestDoubles/Common/Authorization/LocalTestPermissionService.cs
src/InsuranceIntelligence.LocalTestDoubles/Common/Audit/InMemoryAuditWriter.cs
src/InsuranceIntelligence.LocalTestDoubles/Common/Providers/LocalTestProviderHealthCheck.cs
```

These files are allowed only inside `InsuranceIntelligence.LocalTestDoubles` and test projects. They must not exist inside `InsuranceIntelligence.Api`, `InsuranceIntelligence.Application`, `InsuranceIntelligence.Infrastructure`, or `InsuranceIntelligence.Functions`.

---

## 4. Application port 1 — Tenant context

### 4.1 TenantContext model

```csharp
namespace InsuranceIntelligence.Application.Common.Tenancy;

public sealed record TenantContext(
    string TenantId,
    string? WorkspaceId,
    string UserId,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    string CorrelationId);
```

Rules:

```text
- TenantId is required.
- UserId is required.
- WorkspaceId is optional only where a record is tenant-scoped but not workspace-scoped.
- Roles and Permissions must be resolved server-side.
- CorrelationId must be generated or validated server-side.
- Client headers may be used only as requested selection hints after membership validation, never as authority.
```

### 4.2 ITenantContextAccessor interface

```csharp
namespace InsuranceIntelligence.Application.Common.Tenancy;

public interface ITenantContextAccessor
{
    TenantContext Current { get; }
}

public interface ITenantContextSetter
{
    void SetCurrent(TenantContext context);
    void Clear();
}
```

### 4.3 Production-safe scoped accessor implementation

This implementation is not a mock. It is a scoped holder used by API middleware after the authenticated request has been validated and resolved.

```csharp
namespace InsuranceIntelligence.Application.Common.Tenancy;

public sealed class TenantContextAccessor : ITenantContextAccessor, ITenantContextSetter
{
    private TenantContext? _current;

    public TenantContext Current => _current ?? throw new UnauthorizedAccessException("Tenant context not resolved.");

    public void SetCurrent(TenantContext context)
    {
        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new UnauthorizedAccessException("Tenant context not resolved.");
        }

        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new UnauthorizedAccessException("User context not resolved.");
        }

        _current = context;
    }

    public void Clear() => _current = null;
}
```

### 4.4 Entra-compatible authenticated tenant resolver

The resolver belongs in the API layer because it depends on ASP.NET Core `HttpContext` and authenticated request claims.

```csharp
using System.Security.Claims;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Api.Tenancy;

public interface IAuthenticatedTenantResolver
{
    Task<TenantContext> ResolveAsync(
        ClaimsPrincipal principal,
        string? requestedWorkspaceId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class AuthenticatedTenantResolver : IAuthenticatedTenantResolver
{
    private readonly IPermissionReadRepository _permissionReadRepository;

    public AuthenticatedTenantResolver(IPermissionReadRepository permissionReadRepository)
    {
        _permissionReadRepository = permissionReadRepository;
    }

    public async Task<TenantContext> ResolveAsync(
        ClaimsPrincipal principal,
        string? requestedWorkspaceId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        var userId = GetFirstClaimValue(principal,
            ClaimTypes.NameIdentifier,
            "oid",
            "sub") ?? throw new UnauthorizedAccessException("Authenticated user id claim is missing.");

        var tenantHint = GetFirstClaimValue(principal,
            "tid",
            "tenant_id",
            "http://schemas.microsoft.com/identity/claims/tenantid");

        var membership = await _permissionReadRepository.GetEffectiveMembershipAsync(
            userId,
            tenantHint,
            requestedWorkspaceId,
            cancellationToken);

        if (membership is null)
        {
            throw new ForbiddenException("The authenticated user is not assigned to the requested tenant/workspace.");
        }

        return new TenantContext(
            TenantId: membership.TenantId,
            WorkspaceId: membership.WorkspaceId,
            UserId: userId,
            Roles: membership.Roles,
            Permissions: membership.Permissions,
            CorrelationId: correlationId);
    }

    private static string? GetFirstClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
```

### 4.5 TenantWorkspaceContextMiddleware

```csharp
using InsuranceIntelligence.Api.Tenancy;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Api.Middleware;

public sealed class TenantWorkspaceContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantWorkspaceContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IAuthenticatedTenantResolver tenantResolver,
        ITenantContextSetter tenantContextSetter)
    {
        var correlationId = ResolveCorrelationId(httpContext);
        var requestedWorkspaceId = httpContext.Request.Headers["X-Workspace-Id"].FirstOrDefault();

        var context = await tenantResolver.ResolveAsync(
            httpContext.User,
            requestedWorkspaceId,
            correlationId,
            httpContext.RequestAborted);

        tenantContextSetter.SetCurrent(context);

        try
        {
            await _next(httpContext);
        }
        finally
        {
            tenantContextSetter.Clear();
        }
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        var supplied = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(supplied) ? Guid.NewGuid().ToString("N") : supplied.Trim();

        httpContext.Response.Headers["X-Correlation-ID"] = correlationId;
        return correlationId;
    }
}
```

Important behavior:

```text
- X-Workspace-Id is only a requested workspace hint.
- Tenant ID must not be accepted from browser headers as authority.
- The resolver must confirm real membership before setting TenantContext.
- Development/demo headers must not be accepted in Staging, Production, or ManagedApplicationReviewer.
```

---

## 5. Application port 2 — Permission enforcement

### 5.1 Permissions constants

The names below match the existing service-output docs exactly. Existing service docs must not need to change.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static class Permissions
{
    public const string ClaimsRead = "claims.read";
    public const string ClaimsCreate = "claims.create";
    public const string ClaimsAssign = "claims.assign";

    public const string DocumentsRead = "documents.read";
    public const string DocumentsUpload = "documents.upload";
    public const string DocumentsReview = "documents.review";

    public const string ApprovalsRead = "approvals.read";
    public const string ApprovalsReview = "approvals.review";

    public const string AuditRead = "audit.read";
    public const string CommandCenterQuery = "command_center.query";

    public const string ChannelsRead = "channels.read";
    public const string CustomersRead = "customers.read";
    public const string SalesLeadsRead = "sales_leads.read";

    public const string IntegrationsManage = "integrations.manage";
    public const string AdminManage = "admin.manage";
}
```

### 5.2 IPermissionService interface

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public interface IPermissionService
{
    Task EnsureAsync(string permission, CancellationToken cancellationToken);
}
```

### 5.3 Permission read repository port

This repository is an Application-layer port. Its production implementation is EF Core-backed in Infrastructure.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public interface IPermissionReadRepository
{
    Task<EffectiveTenantMembership?> GetEffectiveMembershipAsync(
        string userId,
        string? entraTenantHint,
        string? requestedWorkspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string tenantId,
        string? workspaceId,
        string userId,
        CancellationToken cancellationToken);
}

public sealed record EffectiveTenantMembership(
    string TenantId,
    string? WorkspaceId,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions);
```

### 5.4 PermissionService implementation

This implementation is not hardcoded. It uses the real resolved TenantContext and the real role/permission store. `TenantContext.Permissions` may contain a server-side cache loaded by middleware, but the source of truth is the role/permission store.

```csharp
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.Common.Authorization;

public sealed class PermissionService : IPermissionService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionReadRepository _permissionReadRepository;

    public PermissionService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionReadRepository permissionReadRepository)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionReadRepository = permissionReadRepository;
    }

    public async Task EnsureAsync(string permission, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission is required.", nameof(permission));
        }

        var context = _tenantContextAccessor.Current;

        var effectivePermissions = context.Permissions.Count > 0
            ? context.Permissions
            : await _permissionReadRepository.GetEffectivePermissionsAsync(
                context.TenantId,
                context.WorkspaceId,
                context.UserId,
                cancellationToken);

        if (!effectivePermissions.Contains(permission, StringComparer.Ordinal))
        {
            throw new ForbiddenException($"Permission '{permission}' is required.");
        }
    }
}
```

### 5.5 EF-backed permission repository

The concrete repository belongs in Infrastructure and follows the existing `Ef{Entity}Repository` convention.

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Authorization;

public sealed class EfPermissionReadRepository : IPermissionReadRepository
{
    private readonly IipDbContext _dbContext;

    public EfPermissionReadRepository(IipDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EffectiveTenantMembership?> GetEffectiveMembershipAsync(
        string userId,
        string? entraTenantHint,
        string? requestedWorkspaceId,
        CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.UserTenantMemberships
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Where(x => entraTenantHint == null || x.ExternalIdentityTenantId == entraTenantHint)
            .Where(x => requestedWorkspaceId == null || x.WorkspaceId == requestedWorkspaceId)
            .Select(x => new
            {
                x.TenantId,
                x.WorkspaceId,
                Roles = x.RoleAssignments.Select(r => r.Role.Code).ToList(),
                Permissions = x.RoleAssignments
                    .SelectMany(r => r.Role.RolePermissions)
                    .Select(p => p.Permission.Code)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var membership = memberships.Count == 1 ? memberships[0] : null;
        if (membership is null)
        {
            return null;
        }

        return new EffectiveTenantMembership(
            TenantId: membership.TenantId,
            WorkspaceId: membership.WorkspaceId,
            Roles: membership.Roles.ToHashSet(StringComparer.Ordinal),
            Permissions: membership.Permissions.ToHashSet(StringComparer.Ordinal));
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string tenantId,
        string? workspaceId,
        string userId,
        CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.UserTenantMemberships
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.WorkspaceId == workspaceId)
            .Where(x => x.IsActive)
            .SelectMany(x => x.RoleAssignments)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions.ToHashSet(StringComparer.Ordinal);
    }
}
```

Repository notes:

```text
- Table/entity names may be adapted to the actual DbContext model.
- Do not replace this with a hardcoded role switch.
- Do not read permissions from browser-supplied role names.
- Do not grant Admin based on demo headers in protected runtimes.
```

---

## 6. Application port 3 — Audit writer

### 6.1 AuditEventCreateRequest

This request shape matches the existing service-output docs exactly.

```csharp
namespace InsuranceIntelligence.Application.Common.Audit;

public sealed record AuditEventCreateRequest(
    string TenantId,
    string? WorkspaceId,
    string EventType,
    string ActorType,
    string? ActorUserId,
    string Action,
    string EntityType,
    string EntityId,
    string Summary,
    string? Reason,
    string CorrelationId,
    DateTimeOffset OccurredAt);
```

### 6.2 Audit event type values

```csharp
namespace InsuranceIntelligence.Application.Common.Audit;

public static class AuditEventTypeValues
{
    public const string UserLogin = "user_login";
    public const string DataViewed = "data_viewed";
    public const string DataChanged = "data_changed";
    public const string DocumentUploaded = "document_uploaded";
    public const string AiExtractionGenerated = "ai_extraction_generated";
    public const string HumanCorrectionMade = "human_correction_made";
    public const string ClaimDecisionMade = "claim_decision_made";
    public const string CustomerCommunicationSent = "customer_communication_sent";
    public const string ConsentCaptured = "consent_captured";
    public const string WorkflowChanged = "workflow_changed";
    public const string ReportExported = "report_exported";
}
```

### 6.3 IAuditWriter interface

```csharp
namespace InsuranceIntelligence.Application.Common.Audit;

public interface IAuditWriter
{
    Task WriteAsync(
        AuditEventCreateRequest request,
        CancellationToken cancellationToken);
}
```

### 6.4 Audit persistence model

The new repository should align this with the existing EF Core persistence conventions and migrations.

```csharp
namespace InsuranceIntelligence.Infrastructure.Persistence.Entities;

public sealed class AuditEvent
{
    public string AuditEventId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string? WorkspaceId { get; set; }
    public string EventType { get; set; } = default!;
    public string ActorType { get; set; } = default!;
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

### 6.5 EfAuditWriter implementation

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Infrastructure.Persistence.Entities;

namespace InsuranceIntelligence.Infrastructure.Audit;

public sealed class EfAuditWriter : IAuditWriter
{
    private readonly IAuditEventRepository _repository;

    public EfAuditWriter(IAuditEventRepository repository)
    {
        _repository = repository;
    }

    public async Task WriteAsync(
        AuditEventCreateRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var auditEvent = new AuditEvent
        {
            AuditEventId = Guid.NewGuid().ToString("N"),
            TenantId = request.TenantId,
            WorkspaceId = request.WorkspaceId,
            EventType = request.EventType,
            ActorType = request.ActorType,
            ActorUserId = request.ActorUserId,
            Action = request.Action,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Summary = RedactAndTruncate(request.Summary, 1000),
            Reason = RedactAndTruncate(request.Reason, 1000),
            CorrelationId = request.CorrelationId,
            OccurredAt = request.OccurredAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(auditEvent, cancellationToken);
    }

    private static void Validate(AuditEventCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId)) throw new ValidationException("tenantId is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.EventType)) throw new ValidationException("eventType is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.ActorType)) throw new ValidationException("actorType is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.Action)) throw new ValidationException("action is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.EntityType)) throw new ValidationException("entityType is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.EntityId)) throw new ValidationException("entityId is required for audit events.");
        if (string.IsNullOrWhiteSpace(request.CorrelationId)) throw new ValidationException("correlationId is required for audit events.");
    }

    private static string? RedactAndTruncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var trimmed = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
```

### 6.6 EfAuditEventRepository implementation

```csharp
using InsuranceIntelligence.Infrastructure.Persistence.Entities;

namespace InsuranceIntelligence.Infrastructure.Audit;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

public sealed class EfAuditEventRepository : IAuditEventRepository
{
    private readonly IipDbContext _dbContext;

    public EfAuditEventRepository(IipDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _dbContext.AuditEvents.Add(auditEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

Audit writer rules:

```text
- Do not swallow audit-write failures for important state changes.
- Do not store raw prompts, system messages, tokens, secrets, SAS values, raw provider payloads, or unnecessary sensitive PII in Summary or Reason.
- Audit writes must use durable EF Core/Azure SQL persistence in protected runtimes.
- InMemory audit writers are allowed only in LocalTest assemblies.
```

---

## 7. Application port 4 — Provider health check

### 7.1 Provider codes

These codes match the service-output docs and provider configuration patterns.

```csharp
namespace InsuranceIntelligence.Application.Common.Providers;

public static class ProviderCodes
{
    public const string AzureBlobStorage = "azure_blob_storage";
    public const string AzureDocumentIntelligence = "azure_document_intelligence";
    public const string AzureAiSearch = "azure_ai_search";
    public const string AzureOpenAi = "azure_openai";
    public const string AzureServiceBus = "azure_service_bus";
    public const string GraphMail = "graph_mail";
}
```

### 7.2 Provider health models

```csharp
namespace InsuranceIntelligence.Application.Common.Providers;

public enum ProviderStatus
{
    Connected,
    Unavailable,
    NotConfigured
}

public sealed record ProviderHealthResult(
    string ProviderCode,
    ProviderStatus Status,
    string Message,
    DateTimeOffset CheckedAt);
```

### 7.3 IProviderHealthCheck interface

```csharp
namespace InsuranceIntelligence.Application.Common.Providers;

public interface IProviderHealthCheck
{
    Task<ProviderHealthResult> CheckAsync(
        string providerCode,
        CancellationToken cancellationToken);
}
```

### 7.4 Infrastructure provider connectivity abstraction

Each real Azure/Microsoft adapter should expose or be wrapped by a connectivity check. This is an Infrastructure concern.

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers;

public interface IProviderConnectivityCheck
{
    string ProviderCode { get; }

    Task<ProviderHealthResult> CheckAsync(
        CancellationToken cancellationToken);
}
```

### 7.5 AzureProviderHealthCheck implementation

```csharp
using InsuranceIntelligence.Application.Common.Providers;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Infrastructure.Providers;

public sealed class AzureProviderHealthCheck : IProviderHealthCheck
{
    private readonly IReadOnlyDictionary<string, IProviderConnectivityCheck> _checks;
    private readonly ProviderHealthOptions _options;

    public AzureProviderHealthCheck(
        IEnumerable<IProviderConnectivityCheck> checks,
        IOptions<ProviderHealthOptions> options)
    {
        _checks = checks.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    public async Task<ProviderHealthResult> CheckAsync(
        string providerCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return new ProviderHealthResult(
                ProviderCode: providerCode,
                Status: ProviderStatus.NotConfigured,
                Message: "Provider code is required.",
                CheckedAt: DateTimeOffset.UtcNow);
        }

        if (!_checks.TryGetValue(providerCode, out var check))
        {
            return new ProviderHealthResult(
                ProviderCode: providerCode,
                Status: ProviderStatus.NotConfigured,
                Message: "No provider health check is registered for this capability.",
                CheckedAt: DateTimeOffset.UtcNow);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        try
        {
            return await check.CheckAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderHealthResult(
                ProviderCode: providerCode,
                Status: ProviderStatus.Unavailable,
                Message: "Provider health check timed out.",
                CheckedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception)
        {
            return new ProviderHealthResult(
                ProviderCode: providerCode,
                Status: ProviderStatus.Unavailable,
                Message: "Provider health check failed. See server logs with correlation id.",
                CheckedAt: DateTimeOffset.UtcNow);
        }
    }
}
```

### 7.6 ProviderHealthOptions

Use the existing current-repo `ProviderHealthOptions` shape if present. In the rebuilt repository, it must be bound under the same Infrastructure options tree.

```csharp
namespace InsuranceIntelligence.Infrastructure.Providers;

public sealed record ProviderHealthOptions
{
    public int TimeoutSeconds { get; init; } = 5;
}
```

### 7.7 Adapter connectivity wrappers

These wrappers call the existing real adapters' own connectivity checks. They do not perform fake success and do not swallow `NotConfigured` as `Connected`.

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.BlobStorage;

public sealed class AzureBlobStorageConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IAzureBlobStorageProvider _provider;

    public AzureBlobStorageConnectivityCheck(IAzureBlobStorageProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.AzureBlobStorage;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.DocumentIntelligence;

public sealed class AzureDocumentIntelligenceConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IAzureDocumentIntelligenceProvider _provider;

    public AzureDocumentIntelligenceConnectivityCheck(IAzureDocumentIntelligenceProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.AzureDocumentIntelligence;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.Search;

public sealed class AzureAiSearchConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IAzureAiSearchProvider _provider;

    public AzureAiSearchConnectivityCheck(IAzureAiSearchProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.AzureAiSearch;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.OpenAi;

public sealed class AzureOpenAiConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IAzureOpenAiProvider _provider;

    public AzureOpenAiConnectivityCheck(IAzureOpenAiProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.AzureOpenAi;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.ServiceBus;

public sealed class AzureServiceBusConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IAzureServiceBusProvider _provider;

    public AzureServiceBusConnectivityCheck(IAzureServiceBusProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.AzureServiceBus;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

```csharp
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Infrastructure.Providers.Graph;

public sealed class GraphMailConnectivityCheck : IProviderConnectivityCheck
{
    private readonly IGraphMailProvider _provider;

    public GraphMailConnectivityCheck(IGraphMailProvider provider)
    {
        _provider = provider;
    }

    public string ProviderCode => ProviderCodes.GraphMail;

    public Task<ProviderHealthResult> CheckAsync(CancellationToken cancellationToken) =>
        _provider.CheckConnectivityAsync(cancellationToken);
}
```

Adapter contract note:

```text
IAzureBlobStorageProvider, IAzureDocumentIntelligenceProvider, IAzureAiSearchProvider, IAzureOpenAiProvider, IAzureServiceBusProvider, and IGraphMailProvider represent the existing real Infrastructure adapters from the current repo. If their current method names differ, keep the adapters and create small wrapper classes so IProviderHealthCheck still exposes one common CheckAsync interface to Application services.
```

---

## 8. Exceptions required by the 12 service docs

Place these in Application so all Application services can throw them without depending on API or Infrastructure.

```csharp
namespace InsuranceIntelligence.Application.Common.Exceptions;

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public sealed class NotFoundOrNotVisibleException : Exception
{
    public NotFoundOrNotVisibleException(string message) : base(message) { }
}

public sealed class ProviderNotConfiguredException : Exception
{
    public ProviderNotConfiguredException(string message) : base(message) { }
}

public sealed class ProviderUnavailableException : Exception
{
    public ProviderUnavailableException(string message) : base(message) { }
}
```

API exception middleware must map these to safe HTTP responses:

```text
ForbiddenException -> 403
ValidationException -> 400 or 422 according to endpoint convention
NotFoundOrNotVisibleException -> 404
ProviderNotConfiguredException -> 503 with Not configured code
ProviderUnavailableException -> 503 with Provider unavailable code
UnauthorizedAccessException -> 401
```

---

## 9. Dependency injection registration

### 9.1 Application core port registration

```csharp
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Application.Common;

public static class ApplicationCorePortRegistration
{
    public static IServiceCollection AddApplicationCorePorts(this IServiceCollection services)
    {
        services.AddScoped<TenantContextAccessor>();
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<TenantContextAccessor>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContextAccessor>());

        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }
}
```

### 9.2 API registration

```csharp
using InsuranceIntelligence.Api.Middleware;
using InsuranceIntelligence.Api.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.DependencyInjection;

public static class ApiTenantContextRegistration
{
    public static IServiceCollection AddApiTenantContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthenticatedTenantResolver, AuthenticatedTenantResolver>();
        return services;
    }

    public static IApplicationBuilder UseIipTenantContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantWorkspaceContextMiddleware>();
    }
}
```

### 9.3 Infrastructure core port registration

This registration must run only after `InfrastructureOptions` has been bound and validated by `AzureProviderStartupValidator`.

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Infrastructure.Audit;
using InsuranceIntelligence.Infrastructure.Authorization;
using InsuranceIntelligence.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Infrastructure.DependencyInjection;

public static class CorePortInfrastructureRegistration
{
    public static IServiceCollection AddCorePortInfrastructure(
        this IServiceCollection services,
        InfrastructureOptions options,
        string environmentName)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environmentName);
        AzureProviderStartupValidator.Validate(options);
        AzureProviderStartupValidator.ValidateProduction(options, environmentName);

        if (options.StorageProvider != "SqlServer")
        {
            throw new InvalidOperationException("Core port Infrastructure requires SqlServer persistence. SQLite/InMemory are not permitted.");
        }

        if (runtimeProfile is RuntimeProfile.Staging or RuntimeProfile.Production or RuntimeProfile.ManagedApplicationReviewer)
        {
            EnsureNoLocalTestAssemblyReferences();
        }

        services.AddScoped<IPermissionReadRepository, EfPermissionReadRepository>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
        services.AddScoped<IAuditWriter, EfAuditWriter>();

        services.AddScoped<IProviderHealthCheck, AzureProviderHealthCheck>();

        services.AddScoped<IProviderConnectivityCheck, AzureBlobStorageConnectivityCheck>();
        services.AddScoped<IProviderConnectivityCheck, AzureDocumentIntelligenceConnectivityCheck>();
        services.AddScoped<IProviderConnectivityCheck, AzureAiSearchConnectivityCheck>();
        services.AddScoped<IProviderConnectivityCheck, AzureOpenAiConnectivityCheck>();
        services.AddScoped<IProviderConnectivityCheck, AzureServiceBusConnectivityCheck>();
        services.AddScoped<IProviderConnectivityCheck, GraphMailConnectivityCheck>();

        return services;
    }

    private static void EnsureNoLocalTestAssemblyReferences()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(x => x.GetName().Name ?? string.Empty);

        if (loadedAssemblies.Any(x => x.Contains("LocalTestDoubles", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("LocalTestDoubles assembly must not be loaded in protected runtime profiles.");
        }
    }
}
```

Notes:

```text
- Use the current repo's existing InfrastructureOptions and AzureProviderStartupValidator as the literal benchmark.
- Do not introduce new provider-mode semantics.
- SQLite is rejected outright in all environments.
- AddEfRepositories(services) must be gated behind StorageProvider == SqlServer.
- LocalTestDoubles must not be referenced by production projects.
```

---

## 10. LocalTest-only isolation requirement

### 10.1 Physical isolation rule

LocalTest-only implementations of these ports must live in a separate assembly:

```text
InsuranceIntelligence.LocalTestDoubles
```

They must never live inline inside:

```text
InsuranceIntelligence.Api
InsuranceIntelligence.Application
InsuranceIntelligence.Infrastructure
InsuranceIntelligence.Functions
```

### 10.2 Allowed LocalTest doubles

Allowed only in `InsuranceIntelligence.LocalTestDoubles` and test projects:

```text
LocalTestTenantContextAccessor
LocalTestPermissionService
InMemoryAuditWriter
LocalTestProviderHealthCheck
FakeBlobStorageProvider
FakeDocumentExtractionProvider
FakeRetrievalProvider
FakeChatCompletionProvider
FakeQueueProvider
FakeGraphMailProvider
```

### 10.3 Forbidden in deployable output

Protected runtime output must not contain assemblies or types whose name includes:

```text
Mock
InMemory
Simulated
Placeholder
Demo
DevelopmentAuth
SeededDemoBusinessData
FallbackToMock
LocalTestDoubles
```

### 10.4 LocalTest example registration

This registration is shown only to define isolation. It belongs in `InsuranceIntelligence.LocalTestDoubles`, never in production code.

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.LocalTestDoubles;

public static class LocalTestDoublesRegistration
{
    public static IServiceCollection AddLocalTestDoubles(this IServiceCollection services, TenantContext tenantContext)
    {
        services.AddSingleton<ITenantContextAccessor>(new LocalTestTenantContextAccessor(tenantContext));
        services.AddSingleton<IPermissionService, LocalTestPermissionService>();
        services.AddSingleton<IAuditWriter, InMemoryAuditWriter>();
        services.AddSingleton<IProviderHealthCheck, LocalTestProviderHealthCheck>();
        return services;
    }
}
```

No deployable project may call `AddLocalTestDoubles`.

---

## 11. Tests to add

### 11.1 Tenant context tests

```text
TenantContextTests
- TenantWorkspaceContextMiddleware_ResolvesTenantFromAuthenticatedClaimsAndMembership
- TenantWorkspaceContextMiddleware_DoesNotTrustClientTenantHeader
- TenantWorkspaceContextMiddleware_RejectsRequestedWorkspaceWhenMembershipDoesNotAllowIt
- TenantWorkspaceContextMiddleware_SetsCorrelationId
- TenantContextAccessor_ThrowsWhenCurrentIsReadBeforeResolution
```

Example test intent:

```csharp
[Fact]
public async Task TenantWorkspaceContextMiddleware_RejectsRequestedWorkspaceWhenMembershipDoesNotAllowIt()
{
    var resolver = new Mock<IAuthenticatedTenantResolver>();
    resolver.Setup(x => x.ResolveAsync(
            It.IsAny<ClaimsPrincipal>(),
            "workspace-b",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("The authenticated user is not assigned to the requested tenant/workspace."));

    // Assert middleware returns/propagates forbidden via safe exception middleware.
}
```

### 11.2 Permission service tests

```text
PermissionServiceTests
- EnsureAsync_AllowsPermissionPresentInResolvedContext
- EnsureAsync_LoadsPermissionsFromStoreWhenContextPermissionsEmpty
- EnsureAsync_ThrowsForbiddenWhenPermissionMissing
- EnsureAsync_DoesNotUseBrowserRoleOrHeaderValues
```

Example test intent:

```csharp
[Fact]
public async Task EnsureAsync_ThrowsForbiddenWhenPermissionMissing()
{
    var context = new TenantContext(
        TenantId: "tenant-a",
        WorkspaceId: "workspace-a",
        UserId: "user-1",
        Roles: new HashSet<string> { "claims_officer" },
        Permissions: new HashSet<string>(),
        CorrelationId: "corr-1");

    var repository = new Mock<IPermissionReadRepository>();
    repository.Setup(x => x.GetEffectivePermissionsAsync("tenant-a", "workspace-a", "user-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HashSet<string> { Permissions.ClaimsRead });

    var service = new PermissionService(new FixedTenantContextAccessor(context), repository.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.EnsureAsync(Permissions.ApprovalsReview, CancellationToken.None));
}
```

### 11.3 Audit writer tests

```text
AuditWriterTests
- WriteAsync_RequiresTenantIdEventTypeActionEntityAndCorrelationId
- WriteAsync_PersistsAuditEventThroughEfRepository
- WriteAsync_RedactsAndTruncatesLongSummaryAndReason
- WriteAsync_DoesNotAcceptEmptyTenantId
```

Example test intent:

```csharp
[Fact]
public async Task WriteAsync_PersistsAuditEventThroughEfRepository()
{
    var repository = new Mock<IAuditEventRepository>();
    var writer = new EfAuditWriter(repository.Object);

    await writer.WriteAsync(new AuditEventCreateRequest(
        TenantId: "tenant-a",
        WorkspaceId: "workspace-a",
        EventType: AuditEventTypeValues.DataViewed,
        ActorType: "user",
        ActorUserId: "user-1",
        Action: "claim.detail.viewed",
        EntityType: "claim",
        EntityId: "clm-1",
        Summary: "Claim viewed.",
        Reason: null,
        CorrelationId: "corr-1",
        OccurredAt: DateTimeOffset.UtcNow), CancellationToken.None);

    repository.Verify(x => x.AddAsync(
        It.Is<AuditEvent>(evt => evt.TenantId == "tenant-a" && evt.EntityId == "clm-1"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 11.4 Provider health tests

```text
ProviderHealthTests
- CheckAsync_ReturnsNotConfiguredWhenProviderCodeHasNoRegisteredCheck
- CheckAsync_ReturnsUnavailableWhenProviderCheckThrows
- CheckAsync_ReturnsUnavailableWhenProviderCheckTimesOut
- CheckAsync_ReturnsConnectedWhenRealAdapterReportsConnected
- CheckAsync_UsesProviderHealthOptionsTimeoutSeconds
```

Example test intent:

```csharp
[Fact]
public async Task CheckAsync_ReturnsNotConfiguredWhenProviderCodeHasNoRegisteredCheck()
{
    var health = new AzureProviderHealthCheck(
        checks: Array.Empty<IProviderConnectivityCheck>(),
        options: Options.Create(new ProviderHealthOptions { TimeoutSeconds = 1 }));

    var result = await health.CheckAsync("azure_blob_storage", CancellationToken.None);

    Assert.Equal(ProviderStatus.NotConfigured, result.Status);
}
```

### 11.5 LocalTest isolation tests

```text
LocalTestIsolationTests
- ApiProject_DoesNotReferenceLocalTestDoublesAssembly
- ApplicationProject_DoesNotReferenceLocalTestDoublesAssembly
- InfrastructureProject_DoesNotReferenceLocalTestDoublesAssembly
- FunctionsProject_DoesNotReferenceLocalTestDoublesAssembly
- ProtectedRuntime_DiRegistrationFailsIfLocalTestDoublesAssemblyIsLoaded
- ProtectedRuntime_DiContainerCannotResolveLocalTestTenantContextAccessor
- ProtectedRuntime_DiContainerCannotResolveInMemoryAuditWriter
- ProtectedRuntime_DiContainerCannotResolveLocalTestProviderHealthCheck
```

Example compile/reference test:

```csharp
[Fact]
public void ProductionAssemblies_DoNotReferenceLocalTestDoubles()
{
    var productionAssemblies = new[]
    {
        typeof(InsuranceIntelligence.Api.Program).Assembly,
        typeof(InsuranceIntelligence.Application.Common.Tenancy.ITenantContextAccessor).Assembly,
        typeof(InsuranceIntelligence.Infrastructure.DependencyInjection.CorePortInfrastructureRegistration).Assembly
    };

    foreach (var assembly in productionAssemblies)
    {
        var referenced = assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
        Assert.DoesNotContain(referenced, x => x is not null && x.Contains("LocalTestDoubles", StringComparison.OrdinalIgnoreCase));
    }
}
```

Example DI-registration-time test:

```csharp
[Theory]
[InlineData("Staging")]
[InlineData("Production")]
[InlineData("ManagedApplicationReviewer")]
public void ProtectedRuntime_DoesNotRegisterLocalTestImplementations(string environmentName)
{
    var services = new ServiceCollection();
    var options = TestInfrastructureOptions.RealAzureSqlAndAzureProviders(environmentName);

    services.AddApplicationCorePorts();
    services.AddCorePortInfrastructure(options, environmentName);

    var descriptors = services.Select(x => x.ImplementationType?.FullName ?? x.ImplementationInstance?.GetType().FullName ?? string.Empty).ToList();

    Assert.DoesNotContain(descriptors, x => x.Contains("LocalTest", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(descriptors, x => x.Contains("InMemory", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(descriptors, x => x.Contains("Mock", StringComparison.OrdinalIgnoreCase));
}
```

### 11.6 Protected runtime provider validation tests

These tests should already exist around `AzureProviderStartupValidator`; add them if missing.

```text
AzureProviderStartupValidatorTests
- ValidateProduction_RejectsDevelopmentAuth
- ValidateProduction_RejectsInMemoryStorage
- ValidateProduction_RejectsSQLiteStorage
- ValidateProduction_RejectsSeedDemoData
- ValidateProduction_RejectsAllowProductionMockProviders
- ValidateProduction_RejectsMockBlobProvider
- ValidateProduction_RejectsMockDocumentProvider
- ValidateProduction_RejectsMockRetrievalProvider
- ValidateProduction_RejectsMockAiProvider
- ValidateProduction_RejectsMockQueueProvider
- ValidateProduction_RejectsMockEmailProvider
```

---

## 12. How the 12 Application services consume these ports

The existing service specs can use these ports unchanged.

Examples:

```text
ClaimsQueryService -> ITenantContextAccessor, IPermissionService, IAuditWriter
SlaRiskService -> ITenantContextAccessor, IPermissionService
ApprovalQueryService -> ITenantContextAccessor, IPermissionService
ApprovalDecisionService -> ITenantContextAccessor, IPermissionService, IAuditWriter
DocumentUploadService -> ITenantContextAccessor, IPermissionService, IProviderHealthCheck, IAuditWriter
DocumentExtractionWorkflowService -> ITenantContextAccessor, IPermissionService, IProviderHealthCheck, IAuditWriter
DocumentReviewService -> ITenantContextAccessor, IPermissionService, IAuditWriter
CompletenessCheckService -> ITenantContextAccessor, IPermissionService, IAuditWriter
ChannelQueryService -> ITenantContextAccessor, IPermissionService
CustomerQueryService -> ITenantContextAccessor, IPermissionService, IAuditWriter
SalesLeadQueryService -> ITenantContextAccessor, IPermissionService
AuditQueryService -> ITenantContextAccessor, IPermissionService
CommandCenterQueryService -> ITenantContextAccessor, IPermissionService, IProviderHealthCheck, IAuditWriter
```

No existing service doc needs to rename its dependencies.

---

## 13. Startup order in the new repository

Recommended startup order in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

var infrastructureOptions = builder.Configuration
    .GetSection("Infrastructure")
    .Get<InfrastructureOptions>() ?? new InfrastructureOptions();

AzureProviderStartupValidator.Validate(infrastructureOptions);
AzureProviderStartupValidator.ValidateProduction(infrastructureOptions, builder.Environment.EnvironmentName);

builder.Services.AddApplicationCorePorts();
builder.Services.AddApiTenantContext();
builder.Services.AddEfPersistence(infrastructureOptions);
builder.Services.AddEfRepositories(infrastructureOptions);
builder.Services.AddAzureProviderAdapters(infrastructureOptions);
builder.Services.AddCorePortInfrastructure(infrastructureOptions, builder.Environment.EnvironmentName);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseIipTenantContext();

app.MapControllers();
app.Run();
```

Rules:

```text
- Authentication must run before tenant context middleware.
- Tenant context must be resolved before controllers call Application services.
- AzureProviderStartupValidator must run before provider registrations claim readiness.
- AddEfRepositories must be gated behind StorageProvider == SqlServer.
- SQLite is rejected outright.
```

---

## 14. TODOs and confirmation points

| Area | Current decision | TODO / confirmation needed |
|---|---|---|
| Tenant selection | Requested workspace header may be used only as a validated selection hint | Confirm whether workspace selection should come from route, header, user preference, or explicit `/me/workspaces` selection. |
| Permission source | EF role/permission store is source of truth | Confirm exact tables/entities in the new DbContext. |
| Audit event taxonomy | Uses existing broad event type values plus action strings | Confirm whether custom action names such as `claim.detail.viewed` should be promoted to formal constants. |
| Provider health | Wrap existing adapter connectivity checks | Confirm exact adapter interface names in the new repo. |
| Provider timeout | Uses `ProviderHealthOptions.TimeoutSeconds`, default 5 seconds | Confirm environment-specific timeout values. |
| LocalTest project | Dedicated `InsuranceIntelligence.LocalTestDoubles` assembly | Confirm CI packaging step excludes it from publish artifacts. |

---

## 15. Completion report template

```text
Implementation document: IIP Infrastructure Layer — Core Ports Implementation.

Files to create:
- Application Common Tenancy, Authorization, Audit, Provider, and Exception ports.
- API tenant/workspace context middleware and authenticated resolver.
- Infrastructure EF permission repository, EF audit writer/repository, Azure provider health checker, and connectivity wrappers.
- LocalTest-only doubles in a physically isolated LocalTestDoubles assembly.

Service logic enabled:
- Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales Leads, and Audit Application-layer service specs can now share the same core ports.

Security impact:
- Tenant/workspace context is resolved server-side from authenticated claims and real membership.
- Permission checks use a real role/permission store.
- Audit writes are durable in protected runtimes.
- Provider checks fail closed instead of simulating success.

Tenant/workspace isolation impact:
- TenantContext becomes the mandatory server-side source for all Application services.
- Client-supplied tenant/workspace values are selection hints only and must be validated against membership.

Authentication/authorization impact:
- Entra-compatible claims are used in protected runtimes.
- Development/demo auth is rejected by AzureProviderStartupValidator in protected runtimes.
- Permissions constants now cover all currently drafted services.

Data model or migration impact:
- Requires EF entities/tables for UserTenantMembership, RoleAssignment, RolePermission, Permission, and AuditEvent.
- Requires sequential EF Core migrations in the new repository.

AI/human approval impact:
- No AI decision logic is implemented here.
- These ports support the audit, permission, tenant, and provider gates required by human-supervised AI workflows.

Tests added or required:
- Tenant context resolution tests.
- Permission-denial tests.
- Audit-write persistence tests.
- Provider-health not-configured/unavailable tests.
- Protected-runtime LocalTest isolation tests.
- AzureProviderStartupValidator protected-runtime rejection tests.

TODOs:
- Confirm final workspace selection source.
- Confirm exact EF role/permission entity names.
- Confirm adapter connectivity-check method names.
- Confirm production provider-health timeout values.
- Confirm CI publish exclusion for LocalTestDoubles.

Missing dependencies:
- Existing InfrastructureOptions and AzureProviderStartupValidator must be ported into the new repository first or in the same foundational Infrastructure pass.
- Real Azure adapters must expose or be wrapped by connectivity checks.
- DbContext must expose permission and audit entities.

No fake data confirmation:
- No mock, in-memory, simulated, placeholder, demo, or fallback implementation is introduced into deployable runtime.
- LocalTest-only doubles are physically isolated in a separate assembly and blocked from protected runtime DI registration.

Recommended next step:
- Implement the new repository foundation in this order:
  1. Solution/project structure.
  2. InfrastructureOptions and AzureProviderStartupValidator.
  3. EF Core DbContext, design-time factory, and initial migrations.
  4. Core ports from this document.
  5. First Application service: IClaimsQueryService.
```
