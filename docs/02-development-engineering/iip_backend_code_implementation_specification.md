# Insurance Intelligence Platform (IIP) - Backend Code Implementation Specification

**Document type:** Backend Code Implementation Specification  
**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Prepared for:** Otic / IIP Product, Engineering, Cloud, Security, AI, QA, Delivery, and Implementation Agents  
**Status:** Implementation-ready backend code blueprint  
**Version:** 1.0  
**Date:** July 2026  
**Classification:** Internal - Engineering, Architecture, AI, Security, and Delivery Reference

---

## 1. Purpose

This document translates the IIP backend architecture, API contracts, canonical data model, AI agent architecture, integration strategy, security rules, and implementation guardrails into a concrete backend code blueprint.

It is intended for Claude, Codex, Antigravity IDE agents, and human engineers who will implement the backend. It should be used together with:

- `AGENTS.md`
- `IIP_IMPLEMENTATION_GUARDRAILS.md`
- `iip_technical_architecture_document.md`
- `iip_api_specification.md`
- `iip_data_model_document.md`
- `iip_ai_agent_architecture_tool_registry.md`
- `iip_ai_rag_knowledge_architecture.md`
- `iip_system_integration_strategy.md`
- `iip_security_compliance_document.md`
- `iip_qa_test_plan.md`

This document is not a replacement for those documents. It is the code-level bridge between architecture and implementation.

---

## 2. Backend Implementation Principle

The backend must enforce IIP's core product principle:

> **AI prepares insurance work. Humans make regulated insurance decisions.**

The backend must never allow AI or automation to autonomously:

- approve a claim;
- reject or repudiate a claim;
- bind a policy;
- set final premium;
- accuse a person or organization of fraud;
- release payment;
- promise settlement;
- issue final complaint response;
- submit regulatory filings without human ownership.

The backend may support AI-assisted:

- summarization;
- classification;
- extraction;
- routing recommendations;
- missing-document detection;
- policy/SOP retrieval;
- customer-safe draft preparation;
- evidence pack generation;
- advisory risk signal creation.

All consequential actions must be permissioned, human-owned, auditable, tenant-scoped, and evidence-linked.

---

## 3. Backend Technology Baseline

| Area | Required baseline |
|---|---|
| Language/runtime | C# / .NET 8 LTS |
| API framework | ASP.NET Core Web API |
| Database access | EF Core with Azure SQL compatibility |
| Background processing | Azure Functions / Durable Functions where needed |
| Messaging | Azure Service Bus |
| File storage | Azure Blob Storage |
| Document extraction | Azure AI Document Intelligence |
| Knowledge retrieval | Azure AI Search |
| AI generation | Azure OpenAI or approved Microsoft Foundry deployment |
| Identity | Microsoft Entra ID / JWT Bearer auth |
| Secrets | Azure Key Vault / Managed Identity / environment references |
| Observability | Application Insights / OpenTelemetry / structured logging |
| API docs | OpenAPI / Swagger in non-production or protected environments |
| Tests | xUnit or NUnit, FluentAssertions, integration tests, security tests |

---

## 4. Required Solution Structure

Use the existing repository structure where present. If the repository differs, adapt carefully without creating duplicate architectures.

Recommended backend solution structure:

```text
src/
  InsuranceIntelligence.Api/
    Controllers/
    Middleware/
    Authorization/
    Filters/
    Contracts/
    OpenApi/
    Program.cs

  InsuranceIntelligence.Application/
    CommandCenter/
    Claims/
    Documents/
    Channels/
    Customers/
    Approvals/
    Audit/
    Integrations/
    Knowledge/
    Ai/
    Reporting/
    SupportHealth/
    Common/

  InsuranceIntelligence.Domain/
    Tenancy/
    Identity/
    Claims/
    Documents/
    Channels/
    Customers/
    Policies/
    Approvals/
    Audit/
    Ai/
    Integrations/
    Knowledge/
    Common/

  InsuranceIntelligence.Infrastructure/
    Persistence/
    Configuration/
    Providers/
    Azure/
    BlobStorage/
    DocumentIntelligence/
    Search/
    OpenAi/
    Graph/
    ServiceBus/
    Telemetry/
    Security/

  InsuranceIntelligence.Functions/
    DocumentIngestion/
    KnowledgeIndexing/
    OutlookIngestion/
    CaseSummary/
    CustomerUpdateDrafting/
    WorkflowTasks/

  InsuranceIntelligence.Tests/
    Unit/
    Application/
    Domain/
    Api/
    Integration/
    Security/
    Ai/
```

---

## 5. Layer Responsibilities

### 5.1 API Layer

Project:

```text
InsuranceIntelligence.Api
```

Responsible for:

- HTTP endpoints;
- authentication setup;
- authorization policies;
- request validation;
- tenant/workspace context resolution;
- correlation ID handling;
- standard response envelopes;
- safe error responses;
- OpenAPI descriptions;
- calling application services;
- never directly calling Azure SDKs or EF Core business logic.

The API layer should be thin. Business workflows should live in the Application layer.

### 5.2 Application Layer

Project:

```text
InsuranceIntelligence.Application
```

Responsible for use cases and orchestration:

- Command Center briefing;
- command query execution;
- claims queue queries;
- FNOL creation;
- document upload registration;
- document extraction workflow initiation;
- case summary generation;
- customer update draft creation;
- approvals;
- audit writing;
- provider health aggregation;
- integration event processing;
- AI tool execution coordination.

This layer coordinates domain rules and infrastructure interfaces, but should not depend directly on concrete Azure SDK implementations.

### 5.3 Domain Layer

Project:

```text
InsuranceIntelligence.Domain
```

Responsible for core insurance rules and entities:

- Tenant and workspace ownership;
- claim status rules;
- document status rules;
- approval state transitions;
- evidence references;
- AI decision boundaries;
- human decision ownership;
- audit event categories;
- value objects;
- domain exceptions.

The Domain layer must not reference:

- ASP.NET Core;
- EF Core;
- Azure SDKs;
- OpenAI SDKs;
- Microsoft Graph SDKs;
- HTTP clients.

### 5.4 Infrastructure Layer

Project:

```text
InsuranceIntelligence.Infrastructure
```

Responsible for concrete providers and persistence:

- EF Core `DbContext`;
- Azure SQL persistence;
- Blob Storage adapter;
- Azure AI Document Intelligence adapter;
- Azure AI Search adapter;
- Azure OpenAI adapter;
- Microsoft Graph mail adapter;
- Azure Service Bus adapter;
- Key Vault / configuration integration;
- Application Insights / telemetry integration.

Concrete adapters must implement interfaces defined in Application or Domain boundary contracts.

### 5.5 Functions / Worker Layer

Project:

```text
InsuranceIntelligence.Functions
```

Responsible for long-running and asynchronous work:

- document ingestion;
- OCR/extraction;
- knowledge indexing;
- Outlook/shared mailbox ingestion;
- case summary jobs;
- customer update draft jobs;
- retries and dead-letter handling;
- workflow timers and SLA escalation jobs.

Functions must still enforce tenant/workspace scope and must write audit events for meaningful workflow actions.

---

## 6. Dependency Direction

Use this dependency direction:

```text
Api -> Application -> Domain
Api -> Infrastructure
Infrastructure -> Application contracts / Domain
Functions -> Application -> Domain
Functions -> Infrastructure
Tests -> all layers as needed
```

Do not allow:

```text
Domain -> Infrastructure
Domain -> Api
Application -> Api
Domain -> Azure SDKs
Domain -> EF Core
Application -> concrete Azure provider implementations
```

---

## 7. Backend Runtime Profiles

The backend must support explicit runtime profiles.

| Profile | Purpose | Test doubles allowed? | Deployable/customer-facing? |
|---|---|---:|---:|
| `LocalTest` | Automated tests and isolated local verification | Yes | No |
| `Development` | Internal development only | Limited and visibly isolated | No |
| `Staging` | Production-candidate validation using synthetic data | No | Yes, internal validation only |
| `Production` | Live insurer/customer operation | No | Yes |
| `ManagedApplicationReviewer` | Partner Center/private-plan review deployment | No | Yes |

Required environment variable:

```bash
INSURANCEINTELLIGENCE_RUNTIME_PROFILE=LocalTest|Development|Staging|Production|ManagedApplicationReviewer
```

In `Staging`, `Production`, and `ManagedApplicationReviewer`, startup/readiness must fail closed if any configured provider resolves to:

```text
Mock
Simulated
Placeholder
InMemory
SQLite
DevelopmentAuth
Demo
SeededDemoBusinessData
FallbackToMock
```

---

## 8. Configuration Validation Code Design

Create a startup validation service:

```text
InsuranceIntelligence.Application.Common.Configuration.IRuntimeConfigurationValidator
InsuranceIntelligence.Infrastructure.Configuration.RuntimeConfigurationValidator
```

Responsibilities:

- read runtime profile;
- check provider modes;
- reject forbidden modes in deployed profiles;
- confirm mandatory settings exist;
- avoid logging secret values;
- produce safe health/readiness errors.

Example interface:

```csharp
public interface IRuntimeConfigurationValidator
{
    RuntimeValidationResult Validate(RuntimeProfile profile);
}

public sealed record RuntimeValidationResult(
    bool IsValid,
    IReadOnlyList<RuntimeValidationIssue> Issues);

public sealed record RuntimeValidationIssue(
    string Code,
    string Message,
    string Severity,
    string? Provider = null);
```

---

## 9. API Layer Design

### 9.1 Controller Groups

Recommended controllers:

```text
Controllers/
  MeController.cs
  CommandCenterController.cs
  ClaimsController.cs
  DocumentsController.cs
  ChannelsController.cs
  CustomersController.cs
  SalesLeadsController.cs
  ApprovalsController.cs
  KnowledgeController.cs
  AuditController.cs
  ReportsController.cs
  IntegrationsController.cs
  SupportHealthController.cs
  AdminController.cs
```

### 9.2 Endpoint Versioning

All product APIs should use:

```text
/api/v1/...
```

Health endpoints may remain:

```text
/health
/health/live
/health/ready
```

### 9.3 Standard API Response Envelope

Use a consistent response envelope:

```csharp
public sealed record ApiResponse<T>(
    T Data,
    ApiResponseMeta Meta);

public sealed record ApiResponseMeta(
    string RequestId,
    string CorrelationId,
    string TenantId,
    string? WorkspaceId,
    DateTimeOffset GeneratedAt);
```

For list responses:

```csharp
public sealed record PagedApiResponse<T>(
    IReadOnlyList<T> Data,
    PaginationMeta Pagination,
    ApiResponseMeta Meta);

public sealed record PaginationMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

### 9.4 Standard Error Response

```csharp
public sealed record ApiErrorResponse(ApiError Error);

public sealed record ApiError(
    string Code,
    string Message,
    string? Target,
    object? Details,
    string TraceId,
    string CorrelationId);
```

Rules:

- Never return stack traces to clients.
- Never return secrets, tokens, connection strings, raw provider exception payloads, prompts, or hidden system messages.
- Use safe business messages.
- Always include correlation ID.

---

## 10. Middleware Design

Create these middleware components:

```text
Middleware/
  CorrelationIdMiddleware.cs
  TenantWorkspaceContextMiddleware.cs
  SafeExceptionHandlingMiddleware.cs
  RequestLoggingMiddleware.cs
```

### 10.1 Correlation ID Middleware

Responsibilities:

- read `X-Correlation-ID` if supplied;
- generate a new ID if missing;
- attach to `HttpContext`;
- include in logs and API responses;
- propagate to downstream services and queue messages.

### 10.2 Tenant/Workspace Context Middleware

Responsibilities:

- resolve tenant and workspace from authenticated user context and database membership;
- reject browser-owned tenant/workspace headers as authority in deployed profiles;
- allow local/demo headers only in explicitly non-deployable local profile;
- attach `TenantContext` to scoped services.

Example abstraction:

```csharp
public interface ITenantContextAccessor
{
    TenantContext Current { get; }
}

public sealed record TenantContext(
    string TenantId,
    string? WorkspaceId,
    string UserId,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    string CorrelationId);
```

### 10.3 Safe Exception Handling Middleware

Responsibilities:

- catch unhandled exceptions;
- log safe structured errors;
- map domain/application exceptions to HTTP status codes;
- never leak internal exception details to clients.

---

## 11. Authorization Design

### 11.1 Backend Is Authority

The frontend may hide navigation and actions for usability, but the backend must enforce every permission.

Do not rely on:

- browser-selected roles;
- demo headers;
- local storage;
- query-string role selection;
- frontend-only route guards.

### 11.2 Permission Model

Create:

```text
Authorization/
  PermissionRequirement.cs
  PermissionAuthorizationHandler.cs
  PermissionPolicyProvider.cs
```

Example permission constants:

```csharp
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
    public const string IntegrationsManage = "integrations.manage";
    public const string CommandCenterQuery = "command_center.query";
}
```

### 11.3 Resource-Level Authorization

For resources such as claims, documents, conversations, approvals, and audit events, authorization must check:

- tenant;
- workspace;
- role;
- permission;
- assignment;
- sensitivity;
- workflow stage.

Example service:

```csharp
public interface IAuthorizationDecisionService
{
    Task EnsureCanReadClaimAsync(string claimId, CancellationToken ct);
    Task EnsureCanReviewDocumentAsync(string documentId, CancellationToken ct);
    Task EnsureCanApproveWorkflowActionAsync(string approvalId, CancellationToken ct);
}
```

---

## 12. Domain Model Design

### 12.1 Required Core Entities

The backend must model at minimum:

```text
Tenant
Workspace
User
Role
Permission
Customer
Policy
Claim
ClaimParty
ClaimEvent
ClaimTask
ClaimNote
Document
DocumentVersion
DocumentExtraction
CompletenessCheck
ChannelConversation
ChannelMessage
SalesLead
Approval
HumanReview
KnowledgeDocument
KnowledgeChunk
AgentRun
AgentRunStep
AiOutput
EvidenceReference
AuditEvent
IntegrationConnection
IntegrationEvent
WriteBackRequest
ProviderHealthSnapshot
UsageMeter
NotificationDraft
```

### 12.2 Entity Base Types

Use base interfaces/classes carefully. Avoid over-abstracting, but enforce common metadata.

```csharp
public interface ITenantScopedEntity
{
    string TenantId { get; }
}

public interface IWorkspaceScopedEntity : ITenantScopedEntity
{
    string? WorkspaceId { get; }
}

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}
```

### 12.3 Human Decision Boundary in Domain

Create explicit domain types that separate AI suggestions from human decisions.

```csharp
public enum DecisionActorType
{
    Human,
    System,
    Agent,
    Integration
}

public enum ConsequentialDecisionType
{
    ClaimApproval,
    ClaimRejection,
    ClaimRepudiation,
    SettlementApproval,
    PaymentRelease,
    PolicyBinding,
    PremiumChange,
    UnderwritingDecision,
    FraudDetermination,
    ComplaintFinalResponse
}
```

Rule:

```text
ConsequentialDecisionType must require DecisionActorType.Human.
```

The backend must not allow `Agent` to be the final actor for consequential decisions.

---

## 13. Persistence Design

### 13.1 DbContext

Create:

```text
Infrastructure/Persistence/IipDbContext.cs
```

Example:

```csharp
public sealed class IipDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentExtraction> DocumentExtractions => Set<DocumentExtraction>();
    public DbSet<ChannelConversation> ChannelConversations => Set<ChannelConversation>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<IntegrationEvent> IntegrationEvents => Set<IntegrationEvent>();

    public IipDbContext(DbContextOptions<IipDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IipDbContext).Assembly);
    }
}
```

### 13.2 EF Core Configurations

Use separate configuration classes:

```text
Infrastructure/Persistence/Configurations/
  ClaimConfiguration.cs
  DocumentConfiguration.cs
  CustomerConfiguration.cs
  AgentRunConfiguration.cs
  AuditEventConfiguration.cs
  IntegrationEventConfiguration.cs
```

### 13.3 Tenant Filtering

Every query must apply tenant and workspace scope explicitly through application services or query helpers.

Do not rely only on frontend filters.

Optional helper:

```csharp
public static class TenantScopedQueryableExtensions
{
    public static IQueryable<T> ForTenant<T>(
        this IQueryable<T> query,
        TenantContext tenantContext)
        where T : class, ITenantScopedEntity
    {
        return query.Where(x => x.TenantId == tenantContext.TenantId);
    }
}
```

Workspace filters should be applied when the record is workspace-scoped.

---

## 14. Repository and Query Pattern

Use one of these patterns consistently:

### Preferred for MVP

Use application services with EF Core directly behind clear service boundaries.

Example:

```text
Application/Claims/ClaimsQueryService.cs
Application/Claims/ClaimsCommandService.cs
```

This avoids unnecessary generic repositories.

### Allowed

Create targeted repositories only where they simplify complex access:

```text
IClaimRepository
IDocumentRepository
IAgentRunRepository
IAuditEventRepository
```

Avoid a generic repository such as:

```text
IRepository<T>
```

unless the codebase already uses it cleanly.

---

## 15. Application Service Design

### 15.1 Command Center Services

Create:

```text
Application/CommandCenter/
  ICommandCenterBriefingService.cs
  ICommandCenterQueryService.cs
  ICommandCenterMetricsService.cs
  ICommandCenterActionProposalService.cs
  IIntentClassifier.cs
  INotificationDraftService.cs
  IProviderRequirementResolver.cs
  Models/
```

Responsibilities:

- return daily briefing;
- return metric pills;
- run chat-first operational queries;
- return structured operational blocks;
- resolve evidence sources;
- prepare action proposals;
- classify intent using deterministic routing first and LLM fallback only when configured;
- resolve intent-specific provider requirements;
- persist customer communication drafts before returning draft IDs;
- enforce role and tenant scope;
- write AI run and audit records where needed, including not-configured query attempts.

### 15.2 Claims Services

```text
Application/Claims/
  IClaimsQueryService.cs
  IClaimIntakeService.cs
  IClaimWorkflowService.cs
  IClaimSummaryService.cs
  ISlaRiskService.cs
```

Responsibilities:

- list claims;
- filter claims by status/SLA/risk/channel;
- create FNOL claim records;
- manage allowed claim workflow transitions;
- return claims needing attention;
- provide evidence-backed summaries.

### 15.3 Document Services

```text
Application/Documents/
  IDocumentUploadService.cs
  IDocumentExtractionWorkflowService.cs
  IDocumentReviewService.cs
  ICompletenessCheckService.cs
```

Responsibilities:

- register uploads;
- store metadata;
- queue extraction;
- process extraction output;
- validate missing documents;
- support human review/correction;
- link evidence to claims.

### 15.4 AI Services

```text
Application/Ai/
  IAgentExecutionService.cs
  IAgentToolRegistry.cs
  IAgentRunWriter.cs
  IAiOutputValidator.cs
  IEvidenceResolver.cs
```

Responsibilities:

- execute allowed agent workflows;
- call scoped tools;
- validate model output;
- attach citations/evidence;
- create `AgentRun` records;
- create audit events.

### 15.5 Audit Services

```text
Application/Audit/
  IAuditWriter.cs
  IAuditQueryService.cs
```

Rules:

- audit events should be append-only;
- audit writes should not silently fail for important actions;
- every AI run, approval, document review, customer send, export, and integration write-back must create audit evidence.

---

## 16. Chat-First Command Center Backend Design

The Command Center is now a chat-first operational workspace. The backend must support structured AI responses, not just plain text.

### 16.1 Required Endpoints

```http
GET  /api/v1/command-center/briefing
GET  /api/v1/command-center/metrics
POST /api/v1/command-center/query
POST /api/v1/command-center/actions/{actionId}/prepare
POST /api/v1/command-center/actions/{actionId}/submit-for-approval
GET  /api/v1/command-center/conversations/{conversationId}
GET  /api/v1/command-center/conversations/{conversationId}/evidence
```

### 16.2 Command Query Request

```csharp
public sealed record CommandCenterQueryRequest(
    string Message,
    string? ConversationId,
    string? CurrentFocusType,
    string? CurrentFocusId,
    IReadOnlyDictionary<string, string>? Filters);
```

### 16.3 Command Query Response

```csharp
public sealed record CommandCenterQueryResponse(
    string ConversationId,
    string MessageId,
    string AssistantSummary,
    IReadOnlyList<CommandCenterBlockDto> Blocks,
    IReadOnlyList<EvidenceSourceDto> EvidenceSources,
    HumanReviewRequirementDto HumanReview,
    DateTimeOffset GeneratedAt);
```

### 16.4 Structured Blocks

The backend should return structured blocks that the frontend can render safely. The wire contract must match the frontend `CommandCenterBlock` discriminated union exactly. JSON serialization must use camelCase property names, so `Type` serializes as `type`, `Markdown` as `markdown`, and so on.

```csharp
public abstract record CommandCenterBlockDto(string Type);

public sealed record TextBlockDto(string Markdown)
    : CommandCenterBlockDto("text");

public sealed record MetricPillStripBlockDto(IReadOnlyList<MetricPillDto> Items)
    : CommandCenterBlockDto("metric_pills");

public sealed record ClaimsTableBlockDto(
    IReadOnlyList<ClaimAttentionRowDto> Rows,
    IReadOnlyDictionary<string, string>? Filters = null)
    : CommandCenterBlockDto("claims_table");

public sealed record SlaSummaryBlockDto(IReadOnlyList<SlaRiskItemDto> Items)
    : CommandCenterBlockDto("sla_summary");

public sealed record DocumentGapListBlockDto(IReadOnlyList<DocumentGapItemDto> Items)
    : CommandCenterBlockDto("document_gap_list");

public sealed record ApprovalQueueBlockDto(IReadOnlyList<ApprovalQueueItemDto> Items)
    : CommandCenterBlockDto("approval_queue");

public sealed record DraftCommunicationBlockDto(
    string DraftId,
    string Preview,
    bool RequiresApproval)
    : CommandCenterBlockDto("draft_communication");

public sealed record ManagementSummaryBlockDto(
    string Title,
    IReadOnlyList<SummarySectionDto> Sections)
    : CommandCenterBlockDto("management_summary");

public sealed record EvidenceListBlockDto(IReadOnlyList<EvidenceSourceDto> Sources)
    : CommandCenterBlockDto("evidence_list");

public sealed record ActionButtonsBlockDto(IReadOnlyList<CommandActionDto> Actions)
    : CommandCenterBlockDto("action_buttons");

public sealed record NotConfiguredBlockDto(
    string Capability,
    string Message,
    string? AdminHref = null)
    : CommandCenterBlockDto("not_configured");

public sealed record ErrorBlockDto(
    string Message,
    string? CorrelationId = null,
    bool? Retryable = null)
    : CommandCenterBlockDto("error");
```

Supporting DTOs used by these block records must match the frontend field names and allowed value sets. PascalCase C# property names are acceptable only when the JSON serializer emits the camelCase names shown in the frontend TypeScript contract.

```csharp
public sealed record ClaimAttentionRowDto(
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    string SlaStatus,
    string Priority,
    string? AssignedTo,
    string LastActivityAt);

public static class CommandCenterSlaStatusValues
{
    public const string OnTrack = "on_track";
    public const string DueSoon = "due_soon";
    public const string Breached = "breached";
}

public static class CommandCenterPriorityValues
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}

public sealed record SlaRiskItemDto(
    string Id,
    string Label,
    string Severity,
    string? DueAt,
    string? Href);

public static class CommandCenterSlaSeverityValues
{
    public const string Warning = "warning";
    public const string Danger = "danger";
}

public sealed record DocumentGapItemDto(
    string Id,
    string? ClaimId,
    string DocumentType,
    string Reason,
    string Status,
    string? Href);

public static class CommandCenterDocumentGapStatusValues
{
    public const string Missing = "missing";
    public const string LowConfidence = "low_confidence";
    public const string NeedsReview = "needs_review";
}

public sealed record ApprovalQueueItemDto(
    string Id,
    string Title,
    string? RequestedBy,
    string Priority,
    bool RequiresHumanDecision,
    string? Href);

public sealed record SummarySectionDto(
    string Heading,
    string Body);

public sealed record CommandActionDto(
    string Id,
    string Label,
    string ActionType,
    bool? RequiresConfirmation,
    bool? RequiresHumanApproval,
    string? Href);

public static class CommandCenterActionTypeValues
{
    public const string OpenRoute = "open_route";
    public const string PrepareDraft = "prepare_draft";
    public const string CreateApprovalTask = "create_approval_task";
    public const string AssignWork = "assign_work";
    public const string RequestDocuments = "request_documents";
    public const string ExportReport = "export_report";
}
```

Allowed value sets:

| Field | Allowed values |
|---|---|
| `slaStatus` | `on_track`, `due_soon`, `breached` |
| `priority` | `low`, `medium`, `high`, `critical` |
| `SlaRiskItem.severity` | `warning`, `danger` |
| `DocumentGapItem.status` | `missing`, `low_confidence`, `needs_review` |
| `CommandAction.actionType` | `open_route`, `prepare_draft`, `create_approval_task`, `assign_work`, `request_documents`, `export_report` |

### 16.5 Metric Pill DTO

Choice made: the backend DTO is trimmed to match the current frontend `MetricPill` wire contract exactly. This prevents runtime rendering mismatches in the Command Center. Richer drilldown fields such as `drilldownRoute`, `drilldownFilters`, `sourceLabel`, and `lastUpdatedAt` may be reintroduced only through a paired backend/frontend contract update.

```csharp
public sealed record MetricPillDto(
    string Label,
    object Value,
    string? Severity,
    string? Href);

public static class CommandCenterMetricSeverityValues
{
    public const string Neutral = "neutral";
    public const string Good = "good";
    public const string Warning = "warning";
    public const string Danger = "danger";
}
```

Rules:

- `Value` must serialize as a JSON string or JSON number only.
- `Severity` must be null or one of `neutral`, `good`, `warning`, `danger`.
- `Href` must be an internal IIP route only. Do not return external URLs for metric drilldowns unless explicitly approved by the security model.

### 16.6 Evidence Source DTO

Choice made: the backend DTO is trimmed to match the current frontend `EvidenceSource` wire contract exactly. Additional backend evidence metadata can remain internal or be added later only through a paired backend/frontend contract update.

```csharp
public sealed record EvidenceSourceDto(
    string Id,
    string Title,
    string SourceType,
    string? Href,
    decimal? Confidence);

public static class CommandCenterEvidenceSourceTypeValues
{
    public const string Claim = "claim";
    public const string Policy = "policy";
    public const string Document = "document";
    public const string Approval = "approval";
    public const string Audit = "audit";
    public const string Interaction = "interaction";
    public const string Report = "report";
    public const string Knowledge = "knowledge";
}
```

Allowed `sourceType` values:

```text
claim
policy
document
approval
audit
interaction
report
knowledge
```

### 16.7 Human Review DTO

```csharp
public sealed record HumanReviewRequirementDto(
    bool Required,
    string Reason,
    IReadOnlyList<string> BlockedAutonomousActions,
    IReadOnlyList<string> AllowedNextActions);
```

### 16.8 Command Center Rules

- The Command Center may answer operational questions from approved, tenant-scoped data.
- It must authorize `command_center.query` before executing any command.
- It must perform additional permission checks before returning claims, documents, approvals, drafts, audit references, or customer communications.
- It must not fabricate metrics.
- It must not return records outside the user's tenant/workspace/role scope.
- It must not return hidden internal notes to unauthorized roles.
- It must distinguish AI-prepared work from human-approved decisions.
- It must include evidence sources where claims/policy/document/approval facts are used.
- It must return `Not configured` blocks where required providers are unavailable.
- Provider checks must be intent-specific. Read-only SQL-backed intents such as SLA breaches, attention claims, missing documents, and pending approvals must not require Azure OpenAI or Azure AI Search unless the intent actually uses them.
- If a provider is not configured, the service must still write an audit event for the attempted query before returning the `not_configured` block.
- AI-generated management summaries and customer communication drafts must pass through an AI output validator before being returned to the frontend.
- Customer communication drafts must be persisted through a backend draft or approval service before a `draft_communication` block returns a `draftId`.
- Timestamps returned to the frontend must use ISO 8601 / round-trip strings, not display-formatted local strings.
- Agent run summaries and audit summaries must be redacted or minimized and must not store raw prompts, full customer communications, hidden notes, or sensitive block payloads unless explicitly required by the audit policy.
- It must never silently fall back to fake operational results.

### 16.9 Cross-Contract Consistency Rule

The backend `CommandCenterBlockDto` type strings and field shapes are the single source of truth for the Command Center wire contract. The frontend `CommandCenterBlock` TypeScript union must be regenerated or manually kept in lockstep with this section. Any future block type addition, discriminator rename, field addition, field removal, enum value change, or field-type change must be added to both documents in the same change.

Any backend response block whose `type` value is not represented in the frontend union will fall through to the frontend unsupported-block case. Any frontend block type not represented by a backend DTO is not production-ready.

Document-level extension: for every IIP module-level wire contract, the backend DTO is the source of truth for the serialized response shape. The matching frontend TypeScript type must be generated from, or manually kept in lockstep with, the backend DTO. Any future field addition, field removal, enum value change, type rename, discriminator rename, or field-type change must update both this backend specification and `IIP_FRONTEND_ARCHITECTURE_AND_AGENT_HANDOFF.md` in the same change, with the module-specific contract tests in Section 26.3 acting as the enforcement mechanism.

## 16A. Core Module Wire Contracts

This section extends the Command Center contract reconciliation pattern to the remaining core modules: Work Queue, Documents, Claims, Approvals, Channels, Customers, Sales & Leads, and Audit.

JSON serialization must use camelCase property names for every DTO in this section. For example, `ClaimId` must serialize as `claimId`, `LastActivityAt` as `lastActivityAt`, and `RequiresHumanDecision` as `requiresHumanDecision`. Enum-like values are represented as strings and must use the exact allowed value sets listed below.

Backend-only fields such as tenant ID, workspace ID, internal authorization context, raw provider payloads, raw OCR output, hidden AI prompts, raw system messages, secrets, internal risk flags, and audit metadata not represented in the frontend types must remain excluded from these wire DTOs unless the matching frontend type is intentionally extended in the same contract change.

### 16A.1 Work Queue contract

Endpoint returning this shape:

```http
GET /api/v1/work-queue
```

```csharp
public sealed record WorkItemDto(
    string Id,
    string Reference,
    string Type,
    string Title,
    string? CustomerName,
    string? Channel,
    string Priority,
    string SlaStatus,
    string? AssignedTo,
    decimal? AiConfidence,
    string Status,
    string LastActivityAt);

public static class WorkItemTypeValues
{
    public const string Claim = "claim";
    public const string Lead = "lead";
    public const string DocumentReview = "document_review";
    public const string Complaint = "complaint";
    public const string ServiceRequest = "service_request";
    public const string Underwriting = "underwriting";
    public const string Approval = "approval";
    public const string IntegrationException = "integration_exception";
}

public static class WorkItemStatusValues
{
    public const string New = "new";
    public const string Assigned = "assigned";
    public const string InReview = "in_review";
    public const string WaitingCustomer = "waiting_customer";
    public const string WaitingOfficer = "waiting_officer";
    public const string Approved = "approved";
    public const string Closed = "closed";
    public const string Escalated = "escalated";
}

public static class WorkItemSlaStatusValues
{
    public const string OnTrack = "on_track";
    public const string DueSoon = "due_soon";
    public const string Breached = "breached";
}

public static class WorkItemPriorityValues
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}

public static class WorkItemChannelValues
{
    public const string WhatsApp = "whatsapp";
    public const string WebChat = "webchat";
    public const string Sms = "sms";
    public const string Email = "email";
    public const string Broker = "broker";
    public const string Manual = "manual";
}
```

Internal-only fields such as tenant scope, workspace scope, assignment history, authorization visibility, and workflow metadata stay backend-only and are excluded from `WorkItemDto` unless the frontend `WorkItem` type is extended in the same change.

### 16A.2 Documents contract

Endpoint returning this shape:

```http
GET /api/v1/documents/{documentId}/review
```

The document review response must include extracted fields matching `ExtractedFieldDto`.

```csharp
public sealed record ExtractedFieldDto(
    string Id,
    string FieldName,
    string Label,
    string Value,
    decimal Confidence,
    int? SourcePage,
    SourceBoundingBoxDto? SourceBoundingBox,
    string Status);

public sealed record SourceBoundingBoxDto(
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height);

public static class ExtractedFieldStatusValues
{
    public const string Accepted = "accepted";
    public const string NeedsReview = "needs_review";
    public const string Corrected = "corrected";
    public const string Rejected = "rejected";
}
```

Internal-only fields such as raw OCR payloads, provider response JSON, blob paths, tenant/workspace scope, and extraction model metadata stay backend-only and are excluded from `ExtractedFieldDto` unless the frontend `ExtractedField` type is extended in the same change.

### 16A.3 Claims contract

Endpoint returning this shape:

```http
GET /api/v1/claims/{claimId}
```

```csharp
public sealed record ClaimDetailDto(
    string ClaimId,
    string ClaimNumber,
    string PolicyNumber,
    string Status, // TODO: confirm canonical claim status value set.
    string Priority,
    string SlaStatus,
    string? AssignedOfficer,
    string? ClaimSummary,
    IReadOnlyDictionary<string, string>? CustomerDetails, // TODO: confirm structured CustomerProfile reuse.
    IReadOnlyDictionary<string, string>? PolicyContext, // TODO: confirm policy summary fields.
    IReadOnlyDictionary<string, string>? FnolDetails, // TODO: confirm FNOL field set.
    IReadOnlyList<ClaimTimelineItemDto> Timeline,
    IReadOnlyList<ClaimDocumentSummaryDto> Documents,
    IReadOnlyList<ClaimCommunicationSummaryDto> CustomerCommunicationHistory,
    IReadOnlyList<ClaimInternalNoteDto> InternalNotes,
    ClaimAiBriefDto? AiClaimBrief,
    IReadOnlyList<AuditEventRowDto> AuditTrail,
    IReadOnlyList<ApprovalQueueRowDto> HumanApprovalLog,
    IReadOnlyList<ClaimWriteBackHistoryItemDto> WriteBackHistory);

public sealed record ClaimTimelineItemDto(
    string Id,
    string Title,
    string OccurredAt,
    string? Description);

public sealed record ClaimDocumentSummaryDto(
    string DocumentId,
    string DocumentType,
    string Status, // TODO: confirm document status value set for claim detail.
    decimal? Confidence,
    string? Href);

public sealed record ClaimCommunicationSummaryDto(
    string Id,
    string Channel,
    string Summary,
    string LastActivityAt);

public sealed record ClaimInternalNoteDto(
    string Id,
    string? AuthorName,
    string Body,
    string CreatedAt);

public sealed record ClaimAiBriefDto(
    string ShortSummary,
    string? DateOfLoss,
    string? LocationOfLoss,
    string? PolicyStatus,
    string? CoverageContext,
    IReadOnlyList<string> DocumentsReceived,
    IReadOnlyList<string> DocumentsMissing,
    IReadOnlyList<string> LowConfidenceExtractions,
    string? SlaRisk,
    string? RecommendedNextHumanAction,
    IReadOnlyList<EvidenceSourceDto> SourcesUsed);

public sealed record ClaimWriteBackHistoryItemDto(
    string Id,
    string TargetSystem,
    string Status, // TODO: confirm write-back status value set.
    string OccurredAt,
    string? Summary);

public static class ClaimPriorityValues
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}

public static class ClaimSlaStatusValues
{
    public const string OnTrack = "on_track";
    public const string DueSoon = "due_soon";
    public const string Breached = "breached";
}

public static class ClaimCommunicationChannelValues
{
    public const string WhatsApp = "whatsapp";
    public const string WebChat = "webchat";
    public const string Sms = "sms";
    public const string Email = "email";
    public const string CallCenter = "call_center";
    public const string Broker = "broker";
    public const string WebsiteForm = "website_form";
}
```

Internal-only fields such as tenant/workspace scope, raw policy-system data, hidden internal risk flags, privileged investigation notes, and raw provider payloads stay backend-only and are excluded from `ClaimDetailDto` unless the frontend `ClaimDetail` type is extended in the same change.

### 16A.4 Approvals contract

Endpoint returning this shape:

```http
GET /api/v1/approvals/queue
```

```csharp
public sealed record ApprovalQueueRowDto(
    string Id,
    string Title,
    string? RequestedBy,
    string Priority,
    bool RequiresHumanDecision,
    string? Href);

public static class ApprovalPriorityValues
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}
```

Internal-only fields such as approval workflow state, approver IDs, tenant/workspace scope, full decision notes, and audit metadata stay backend-only and are excluded from `ApprovalQueueRowDto` unless the frontend `ApprovalQueueRow` type is extended in the same change.

### 16A.5 Channels contract

Endpoint returning this shape:

```http
GET /api/v1/channels/overview
```

```csharp
public sealed record ChannelConversationRowDto(
    string Id,
    string CustomerName,
    string Channel,
    string DetectedIntent,
    string SuggestedRouting,
    decimal AiConfidence,
    string Status, // TODO: confirm channel conversation status value set.
    string? AssignedTo,
    string LastMessage,
    string LastActivityAt);

public static class ChannelValues
{
    public const string WhatsApp = "whatsapp";
    public const string WebChat = "webchat";
    public const string Sms = "sms";
    public const string Email = "email";
    public const string CallCenter = "call_center";
    public const string Broker = "broker";
    public const string WebsiteForm = "website_form";
}

public static class ChannelDetectedIntentValues
{
    public const string ClaimFnol = "claim_fnol";
    public const string QuoteRequest = "quote_request";
    public const string Renewal = "renewal";
    public const string Complaint = "complaint";
    public const string PolicyInquiry = "policy_inquiry";
    public const string DocumentUpload = "document_upload";
    public const string PaymentInquiry = "payment_inquiry";
    public const string GeneralService = "general_service";
}
```

Internal-only fields such as full message bodies, raw attachments, provider message IDs, hidden risk notes, tenant/workspace IDs, and provider payload metadata stay backend-only and are excluded from `ChannelConversationRowDto` unless the frontend `ChannelConversationRow` type is extended in the same change.

### 16A.6 Customers contract

Endpoint returning this shape:

```http
GET /api/v1/customers/{customerId}
```

```csharp
public sealed record CustomerProfileDto(
    string CustomerId,
    IReadOnlyDictionary<string, string> Identity, // TODO: confirm canonical customer identity fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Policies, // TODO: confirm policy summary row fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Claims, // TODO: confirm claim summary row fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Interactions, // TODO: confirm interaction summary row fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Documents, // TODO: confirm document summary row fields.
    IReadOnlyDictionary<string, string> Consent, // TODO: confirm consent summary fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Payments, // TODO: confirm payment summary row fields.
    IReadOnlyList<IReadOnlyDictionary<string, string>> Opportunities, // TODO: confirm opportunity summary row fields.
    IReadOnlyList<AuditEventRowDto> AuditHistory);
```

The Customer 360 contract intentionally uses dictionary sections because the frontend prose currently identifies the sections but not the confirmed field-level schema. Internal-only fields such as raw policy-system payloads, tenant/workspace IDs, hidden risk notes, payment provider payloads, and sensitive internal metadata stay backend-only until the detailed Customer 360 schema is confirmed.

### 16A.7 Sales & Leads contract

Endpoint returning this shape:

```http
GET /api/v1/sales-leads/{leadId}
```

```csharp
public sealed record SalesLeadDetailDto(
    string LeadId,
    string LeadSource,
    string CustomerNeed,
    string ConversationSummary,
    string ProductFit,
    string QuoteStatus, // TODO: confirm quote status value set.
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<string> FollowUpActions,
    string? AssignedSalesOfficer,
    decimal? ConversionProbability);
```

Internal-only fields such as hidden lead-scoring inputs, model notes, tenant/workspace IDs, raw transcripts, and provider metadata stay backend-only and are excluded from `SalesLeadDetailDto` unless the frontend `SalesLeadDetail` type is extended in the same change.

### 16A.8 Audit contract

Endpoint returning this shape:

```http
GET /api/v1/audit/events
```

```csharp
public sealed record AuditEventRowDto(
    string Id,
    string EventType,
    string? ActorUserId,
    string? TargetType, // TODO: confirm target type value set.
    string? TargetId,
    string Summary,
    string OccurredAt,
    string? CorrelationId);

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

Internal-only fields such as raw before/after payloads, hidden prompts, raw system messages, provider exception details, secrets, and sensitive event detail stay backend-only and are excluded from `AuditEventRowDto`. Detailed audit-event inspection must use a separate contract if needed.

### 16A.9 AI Control Center existing frontend type

The frontend already defines `AgentDefinition`. Although AI Control Center is outside the core-module list reconciled above, the backend should still expose a matching DTO when the agent library endpoint is implemented.

Endpoint returning this shape:

```http
GET /api/v1/ai-control-center/agents
```

```csharp
public sealed record AgentDefinitionDto(
    string Id,
    string Name,
    string Purpose,
    string Status,
    decimal ConfidenceThreshold,
    bool HumanReviewRequired,
    string? LastRunAt,
    string OwnerTeam);

public static class AgentDefinitionStatusValues
{
    public const string Active = "active";
    public const string Draft = "draft";
    public const string Paused = "paused";
    public const string Disabled = "disabled";
}

public static class AgentDefinitionOwnerTeamValues
{
    public const string Claims = "claims";
    public const string Sales = "sales";
    public const string Service = "service";
    public const string Compliance = "compliance";
    public const string Admin = "admin";
}
```

Internal-only model deployment names, prompts, system instructions, secrets, raw logs, and provider configuration stay backend-only unless the frontend `AgentDefinition` type is extended in the same change.

## 16B. Module Contract Reconciliation Summary

| Module | Frontend type name | Backend DTO name | Status |
|---|---|---|---|
| Work Queue | `WorkItem` | `WorkItemDto` | matched |
| Documents | `ExtractedField` | `ExtractedFieldDto` | matched |
| Claims | `ClaimDetail` | `ClaimDetailDto` | newly created; several subsection fields need further confirmation |
| Approvals | `ApprovalQueueRow` | `ApprovalQueueRowDto` | newly created |
| Channels | `ChannelConversationRow` | `ChannelConversationRowDto` | newly created; status value set needs further confirmation |
| Customers | `CustomerProfile` | `CustomerProfileDto` | newly created; detailed Customer 360 field schemas need further confirmation |
| Sales & Leads | `SalesLeadDetail` | `SalesLeadDetailDto` | newly created; quote status value set needs further confirmation |
| Audit | `AuditEventRow` | `AuditEventRowDto` | newly created; target type value set needs further confirmation |
| AI Control Center | `AgentDefinition` | `AgentDefinitionDto` | matched existing frontend type; outside required module-test list |

## 17. Provider Interface Design

Define provider interfaces before concrete adapters.

Recommended interfaces:

```text
Application/Common/Providers/
  IBlobStorageProvider.cs
  IDocumentExtractionProvider.cs
  IRetrievalProvider.cs
  IChatCompletionProvider.cs
  IEmbeddingProvider.cs
  IEmailProvider.cs
  INotificationProvider.cs
  IQueueProvider.cs
  IProviderHealthCheck.cs
```

### 17.1 Document Extraction Provider

```csharp
public interface IDocumentExtractionProvider
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken);
}
```

### 17.2 Retrieval Provider

```csharp
public interface IRetrievalProvider
{
    Task<RetrievalResult> SearchAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken);
}
```

### 17.3 Chat Completion Provider

```csharp
public interface IChatCompletionProvider
{
    Task<ChatCompletionResult> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken);
}
```

### 17.4 Email Provider

```csharp
public interface IEmailProvider
{
    Task<IReadOnlyList<InboundEmailMessage>> GetInboundMessagesAsync(
        MailboxSyncRequest request,
        CancellationToken cancellationToken);

    Task<EmailDraftResult> CreateDraftAsync(
        EmailDraftRequest request,
        CancellationToken cancellationToken);

    Task<EmailSendResult> SendApprovedMessageAsync(
        ApprovedEmailSendRequest request,
        CancellationToken cancellationToken);
}
```

Rules:

- Provider interfaces must be tenant-aware.
- Provider calls must include correlation ID.
- Provider errors must be mapped to safe application errors.
- Provider health must be observable.
- No provider may silently fall back to mock behavior in deployed profiles.

---

## 18. Infrastructure Adapter Design

Concrete adapters should live in:

```text
InsuranceIntelligence.Infrastructure/Providers/
```

Recommended adapters:

```text
AzureBlobStorageProvider
AzureDocumentIntelligenceProvider
AzureAiSearchRetrievalProvider
AzureOpenAiChatCompletionProvider
AzureOpenAiEmbeddingProvider
MicrosoftGraphEmailProvider
AzureServiceBusQueueProvider
ApplicationInsightsTelemetryWriter
```

### 18.1 Adapter Rules

- Keep Azure SDK usage inside Infrastructure only.
- Do not expose raw SDK response objects outside Infrastructure.
- Convert provider results into application DTOs.
- Mask or omit sensitive provider metadata.
- Include retry policies where appropriate.
- Use managed identity where possible.
- Read secrets from Key Vault or secure configuration only.

---

## 19. AI Agent Execution Design

### 19.1 Agent Execution Flow

```text
API request or workflow event
-> Application service validates user/context
-> AgentExecutionService creates AgentRun
-> Agent retrieves approved scoped evidence/tools
-> Provider generates structured output
-> Output validator validates schema and safety
-> EvidenceResolver attaches source references
-> AuditWriter records action
-> Response returned or workflow task created
```

### 19.2 Agent Tool Registry

Create:

```text
Application/Ai/Tools/
  AgentToolRegistry.cs
  IAgentTool.cs
  ClaimReadTool.cs
  DocumentReadTool.cs
  KnowledgeSearchTool.cs
  CustomerUpdateDraftTool.cs
  ApprovalCreateTool.cs
```

Example tool contract:

```csharp
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string SideEffectLevel { get; }
    IReadOnlySet<string> RequiredPermissions { get; }

    Task<AgentToolResult> ExecuteAsync(
        AgentToolRequest request,
        CancellationToken cancellationToken);
}
```

### 19.3 Prohibited Tools

Do not implement generic tools such as:

```text
query_database(sql)
read_any_file(path)
send_email_unapproved(...)
approve_claim(...)
reject_claim(...)
release_payment(...)
accuse_fraud(...)
```

Allowed tools must be narrow, scoped, audited, and permissioned.

---

## 20. Document Processing Backend Design

### 20.1 Document Upload Flow

```text
User uploads document
-> API validates file metadata and permission
-> Blob upload is authorized through backend-controlled flow
-> Document record is created
-> DocumentIngestion message is queued
-> AuditEvent is written
-> UI shows processing status
```

### 20.2 Document Extraction Flow

```text
DocumentIngestion worker receives message
-> Loads document metadata
-> Calls Azure AI Document Intelligence
-> Stores extraction result
-> Updates document status
-> Runs completeness check
-> Creates review task if needed
-> Writes audit event
```

### 20.3 Document Review Rules

- Low-confidence fields require human review.
- Rejected documents require reason.
- Human corrections must be audited.
- Documents must remain linked to source claim/customer/channel.
- Sensitive documents must not be exposed to unauthorized roles.

---

## 21. Integration Backend Design

### 21.1 Integration Fabric

The backend must support these connector patterns:

- API ingestion;
- webhook/event ingestion;
- SFTP/file ingestion;
- manual upload;
- Microsoft Graph mailbox ingestion;
- future policy/claims system read-only lookup;
- controlled export/write-back.

### 21.2 Integration Events

Every inbound/outbound integration event should create an `IntegrationEvent` record.

Minimum fields:

```text
IntegrationEventId
TenantId
WorkspaceId
SourceSystem
EventType
ExternalRecordId
Direction
Status
CorrelationId
IdempotencyKey
ReceivedAt
ProcessedAt
FailureReason
PayloadHash
```

Do not store unnecessary raw sensitive payloads where not required.

### 21.3 Write-Back Requests

Write-back must be explicit and human-approved.

```text
AI prepares suggestion
-> Application validates fields
-> Human approves
-> WriteBackRequest is queued
-> External adapter writes or exports
-> IntegrationEvent and AuditEvent recorded
```

---

## 22. Audit and Telemetry Design

### 22.1 Audit Writer

Create:

```csharp
public interface IAuditWriter
{
    Task WriteAsync(AuditEventCreateRequest request, CancellationToken cancellationToken);
}
```

Required audit categories:

```text
claim.created
claim.status_changed
document.uploaded
document.extracted
document.reviewed
ai.agent_run_started
ai.agent_run_completed
ai.output_used
approval.created
approval.completed
customer_update.drafted
customer_update.sent
integration.event_received
integration.writeback_requested
integration.writeback_completed
admin.configuration_changed
security.access_denied
```

### 22.2 Telemetry Rules

Log structured fields:

```text
correlationId
tenantId
workspaceId
userId
action
provider
status
durationMs
errorCode
```

Do not log:

- secrets;
- tokens;
- full national IDs;
- raw medical details unless explicitly required and protected;
- hidden prompts;
- system messages;
- raw provider exception payloads;
- confidential staff-only content.

---

## 23. Health and Provider Readiness Design

Create:

```text
Application/SupportHealth/
  IProviderHealthService.cs
  ISystemReadinessService.cs
```

Health endpoints:

```http
GET /health
GET /health/live
GET /health/ready
GET /api/v1/support/health/providers
```

Provider statuses:

```text
connected
degraded
disconnected
not_configured
unknown
```

Rules:

- Missing mandatory providers in deployed profiles should return Not Ready.
- Optional disabled providers should show `not_configured` rather than fake success.
- Health output must not expose secrets.

---

## 24. Validation and Error Handling

### 24.1 Validation

Use FluentValidation or equivalent for request validation.

Recommended folder:

```text
Application/<Module>/Validation/
```

Rules:

- Validate request shape.
- Validate business rules in application/domain layer.
- Return `400` for malformed input.
- Return `422` for domain validation failure.
- Return `409` for invalid workflow transition or idempotency conflict.

### 24.2 Error Types

Recommended exception hierarchy:

```text
DomainException
ValidationException
ForbiddenException
NotFoundOrNotVisibleException
WorkflowConflictException
ProviderNotConfiguredException
ProviderUnavailableException
UnsafeAiOutputException
```

Map to standard HTTP status codes.

---

## 25. Security Code Requirements

Backend implementation must enforce:

- Entra ID authentication in deployed profiles;
- server-side role/permission checks;
- tenant/workspace filters on every sensitive query;
- no direct object reference exposure across tenants;
- safe file upload validation;
- safe email/document content rendering outputs;
- no secrets in responses/logs;
- no production mock providers;
- no frontend-only authorization;
- audit logging for sensitive reads and state changes;
- explicit provider configuration validation.

### 25.1 File Upload Security

Document upload services must validate:

- allowed file types;
- file size limits;
- virus/malware scanning hook where available;
- tenant-scoped storage path;
- metadata consistency;
- no path traversal;
- no direct public blob access unless explicitly signed and time-limited.

---

## 26. Testing Architecture

### 26.1 Test Projects

Recommended test structure:

```text
InsuranceIntelligence.Tests/
  Domain/
  Application/
  Api/
  Integration/
  Security/
  Ai/
```

### 26.2 Required Test Categories

| Test category | Required coverage |
|---|---|
| Domain tests | Claim transitions, document status, approval boundaries, AI decision prohibitions |
| Application tests | Command Center query, claims workflows, document workflows, approvals, audit writing |
| API tests | Status codes, auth, validation, response envelopes, tenant scoping |
| Security tests | Cross-tenant denial, role denial, no demo headers in staging/production profiles |
| AI tests | Missing evidence refusal, citations required, unsafe action blocking |
| Integration tests | Provider health, Service Bus message processing, persistence survival |
| Runtime profile tests | Staging/Production reject mock/SQLite/in-memory/demo-auth settings |

### 26.3 Tests That Must Exist Before Production Candidate

- `StagingRejectsDemoHeadersTest`
- `ProductionRejectsMockProvidersTest`
- `CommandCenterDoesNotReturnCrossTenantClaimsTest`
- `CommandCenterBlockContractMatchesFrontendTypesTest`
- `CommandCenterQueryUsesIntentSpecificProviderGatesTest`
- `CommandCenterNotConfiguredResponseWritesAuditEventTest`
- `CommandCenterDraftCommunicationIsPersistedBeforeDraftBlockTest`
- `CommandCenterEvidenceSourceTypeAllowsApprovalTest`
- `CommandCenterTimestampsSerializeAsIso8601Test`
- `AiCannotApproveClaimTest`
- `CustomerFacingOutputDoesNotExposeInternalRiskFlagsTest`
- `DocumentExtractionRequiresRealProviderInStagingTest`
- `MissingProviderReturnsNotConfiguredOrNotReadyTest`
- `AuditEventCreatedForAgentRunTest`
- `ApprovalRequiresHumanActorTest`

`CommandCenterBlockContractMatchesFrontendTypesTest` may be implemented as a JSON serialization contract test, a generated-types check, or both. It must verify that every concrete `CommandCenterBlockDto` serializes to a `type` value and field set consumable by the frontend `CommandCenterBlock` discriminated union without falling through to the unsupported-block case.

The Command Center query-service tests must also prove that provider readiness checks are intent-specific, not globally enforced for every command; not-configured responses are audited; customer communication drafts are persisted before `draft_communication` blocks are returned; `approval` is an allowed `EvidenceSourceDto.SourceType`; and timestamps serialize as ISO 8601 strings.

#### Module Contract Consistency Tests

Each module contract test may be implemented as a JSON serialization contract test, a generated-types check, or both. Each test must verify that the backend DTO serializes to the exact camelCase field set and enum-like string value set expected by the matching frontend TypeScript type.

- `WorkQueueContractMatchesFrontendTypesTest`
- `DocumentsContractMatchesFrontendTypesTest`
- `ClaimsContractMatchesFrontendTypesTest`
- `ApprovalsContractMatchesFrontendTypesTest`
- `ChannelsContractMatchesFrontendTypesTest`
- `CustomersContractMatchesFrontendTypesTest`
- `SalesLeadsContractMatchesFrontendTypesTest`
- `AuditContractMatchesFrontendTypesTest`

---

## 27. Phase 1-4 Backend Implementation Sequence

### Phase 1 - Backend foundation

Implement:

- solution/project structure;
- configuration validation;
- runtime profiles;
- correlation middleware;
- tenant/workspace context;
- auth and RBAC foundations;
- standard response/error envelopes;
- EF Core DbContext baseline;
- audit writer baseline;
- provider health baseline.

Exit criteria:

- API builds;
- health endpoints work;
- `/api/v1/me` returns authenticated context;
- staging profile rejects forbidden provider modes;
- audit writer can persist events.

### Phase 2 - Core operational APIs

Implement:

- Command Center briefing, metrics, and query endpoints;
- claims list/detail APIs;
- document metadata/upload registration;
- channels overview;
- customers overview;
- approvals queue;
- support/provider health endpoints.

Exit criteria:

- frontend can render real loading/empty/error/not-configured states;
- no hardcoded dashboard records;
- tenant/workspace filters tested.

### Phase 3 - AI and workflow services

Implement:

- AgentExecutionService;
- scoped tool registry;
- Azure AI Search retrieval adapter;
- Azure OpenAI adapter;
- document extraction workflow;
- case summary service;
- customer update draft service;
- approval workflow integration;
- evidence source resolution.

Exit criteria:

- AI output includes evidence or refusal/uncertainty;
- consequential actions require human review;
- all agent runs audited.

### Phase 4 - Security, QA, and production hardening

Implement:

- runtime profile enforcement tests;
- cross-tenant security tests;
- provider readiness validation;
- no mock deployed provider checks;
- safe error output;
- observability dashboards/events;
- integration retries/DLQ support;
- release validation endpoints/reports.

Exit criteria:

- staging can run only with real providers or explicit Not configured/Not Ready;
- no fake success paths;
- security blockers resolved;
- Command Center works with real backend contracts.

---

## 28. Coding Agent Rules

When implementing backend code, agents must:

1. Read `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, and this document first.
2. Inspect existing repository structure before creating new folders.
3. Prefer extending existing valid architecture over creating duplicates.
4. Never introduce fake provider success paths in deployable runtime.
5. Never rely on frontend authorization.
6. Add tests with every non-trivial backend change.
7. Keep AI tools scoped, permissioned, and audited.
8. Keep Domain free of Azure SDK and EF Core dependencies.
9. Use real provider interfaces and adapters.
10. Stop and report manual Azure/Entra/Graph/Key Vault configuration blockers instead of bypassing them.

---

## 29. Backend Pull Request Checklist

Every backend PR should answer:

```text
What changed?
Which API contracts changed?
Which data model/entity changed?
Which migrations were added?
Which permissions are required?
How is tenant/workspace scope enforced?
Which audit events are written?
Which tests were added/updated?
Does this introduce any mock provider into deployed runtime?
Does this allow AI to perform any consequential decision?
Are provider failures handled safely?
Are secrets and sensitive data protected?
```

---

## 30. Recommended First Codex Task

Use this task to begin implementation safely:

```text
Read AGENTS.md, IIP_IMPLEMENTATION_GUARDRAILS.md, and docs/architecture/iip_backend_code_implementation_specification.md.

Implement Phase 1 backend foundation only:
- confirm or create backend solution/project structure;
- add runtime profile enum/configuration validation;
- add correlation ID middleware;
- add tenant/workspace context abstraction;
- add standard API response/error envelope types;
- add provider health/readiness abstractions;
- add audit writer interface and minimal persistence-backed implementation if DbContext exists;
- add tests proving Staging/Production reject mock/demo/in-memory provider settings.

Do not implement fake operational data.
Do not bypass authentication.
Do not build UI.
Do not implement final claim approval/rejection.
Run build and tests, then report files changed, tests run, blockers, and next safe task.
```

---

## 31. Final Reminder

This backend code design exists to make IIP safe to build with AI coding agents.

The backend must be the source of authority for:

- identity;
- permissions;
- tenant/workspace isolation;
- data access;
- AI tool boundaries;
- workflow state;
- human approval;
- auditability;
- provider readiness;
- production zero-simulation enforcement.

The frontend may make the product usable. The backend must make it safe.

