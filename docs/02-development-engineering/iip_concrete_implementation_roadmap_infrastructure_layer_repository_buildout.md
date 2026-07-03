# IIP Concrete Implementation Roadmap — Infrastructure Layer & Repository Build-Out

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Staff application | IIP Operations |
| Document type | Concrete implementation roadmap / repository build-out tracker |
| Status | Implementation planning artifact |
| Date | July 2026 |

---

## 1. Purpose

This document consolidates the concrete implementation work implied by the IIP backend specification set into one ordered engineering checklist. Its purpose is to help the engineering team, Codex, Claude Code, or another implementation agent execute the repository build-out without repeatedly cross-referencing every individual service specification.

The source specifications already define Application-layer interfaces, DTOs, validation rules, business logic, tests, and completion-report expectations. This roadmap does **not** re-paste those implementations. It identifies the missing concrete repository, persistence, and provider-adapter work needed to turn those specifications into a compiling production-grade repository.

## 2. Source documents covered

- `iip_infrastructure_layer_core_ports_implementation.md`
- `iip_backend_wiring_controllers_composition_root_request_pipeline.md`
- `iip_command_center_query_service_updated_logic.md`
- `iip_claims_query_service_implementation_output.md`
- `iip_sla_risk_service_implementation_output.md`
- `iip_approval_query_service_implementation_output.md`
- `iip_approval_decision_service_implementation_output.md`
- `iip_document_upload_service_implementation_output.md`
- `iip_document_extraction_workflow_service_implementation_output.md`
- `iip_document_review_service_implementation_output.md`
- `iip_completeness_check_service_implementation_output.md`
- `iip_channels_query_service_implementation_output.md`
- `iip_customers_query_service_implementation_output.md`
- `iip_sales_leads_query_service_implementation_output.md`
- `iip_audit_query_service_implementation_output.md`
- `iip_claim_intake_workflow_transition_service.md`
- `iip_integration_write_back_execution_service.md`
- `iip_notification_send_execution_service.md`
- `iip_knowledge_base_indexing_ingestion_service.md`
- `iip_reporting_service.md`
- `iip_email_ingestion_dispatch_provider_agnostic_port.md`
- `iip_tenant_agent_capability_policy_structured_admin_configuration.md`
- `iip_conversational_session_context_multi_agent_architecture.md`
- `iip_database_implementation_migration_plan.md`
- `iip_data_model_document.md`

## 3. Shared implementation rules

```text
1. Foundation must come first; no Application service is production-ready until the core ports and protected-runtime validation are real.
2. Every repository implementation must filter by TenantId and WorkspaceId at the query boundary.
3. No repository may fabricate data; empty database results remain empty results.
4. No mock, simulated, in-memory, placeholder, demo, or fallback implementation may live in deployable Api, Application, Infrastructure, or Functions projects.
5. LocalTest doubles may exist only in InsuranceIntelligence.LocalTestDoubles or test projects.
6. AzureProviderStartupValidator must run before protected runtime services are resolved.
7. AI remains advisory only; human approval and ApprovalDecisionGuard rules are not optional.
8. Concrete adapters are registered only when real provider configuration is present; otherwise services return Not configured/unavailable, not fake success.
9. This roadmap tracks build work only; interface names and service contracts remain exactly as already specified.
```

## 4. Status legend

| Status | Meaning |
|---|---|
| Not Started | No verified concrete implementation exists in the real repository yet. |
| In Progress | File/entity/adapter exists but is incomplete or tests are not passing. |
| Implemented | Concrete code exists and compiles. |
| Verified | Build, tests, and tenant-isolation checks pass. |
| Blocked | Work cannot proceed without an external decision, provider credential, library selection, or customer-specific integration detail. |

Initial status for every item in the master tracker is **Not Started**. Any prior sandbox-generated source package should be treated as reference material only until it is reconciled into the actual repository, built, tested, and reviewed.

---

## 5. Foundation — blocks everything else

### 5.1 Solution and project structure

Create or reconcile the agreed solution layout:

```text
src/
  InsuranceIntelligence.Api/
  InsuranceIntelligence.Application/
  InsuranceIntelligence.Domain/
  InsuranceIntelligence.Infrastructure/
  InsuranceIntelligence.Functions/
  InsuranceIntelligence.LocalTestDoubles/
tests/
  InsuranceIntelligence.Tests/
  InsuranceIntelligence.Tests.Integration/   # optional but recommended for EF/provider integration tests
```

Concrete tasks:

| Task | Output | Notes |
|---|---|---|
| Add `InsuranceIntelligence.Application` project | New project referenced by Api, Infrastructure, Functions, Tests | Required because all service specs use `InsuranceIntelligence.Application.*` namespaces. |
| Reconcile existing `Domain`, `Api`, `Infrastructure`, `Functions` | Existing projects remain, namespaces adapted carefully | Do not break existing code while moving contracts into Application. |
| Add isolated `InsuranceIntelligence.LocalTestDoubles` | Test-only project | Deployable projects must not reference it. |
| Add test project references | Unit and integration tests can reference Application/Infrastructure/Api/LocalTestDoubles | Production projects must never reference test doubles. |

### 5.2 Configuration and protected-runtime validation

| Task | Output | Notes |
|---|---|---|
| Port `InfrastructureOptions` | `src/InsuranceIntelligence.Infrastructure/Configuration/InfrastructureOptions.cs` | Keep existing config-driven provider-mode switching. Do not rewrite proven logic. |
| Port `RuntimeProfile` / parser if not already present | Infrastructure or shared configuration namespace from reconciled docs | Must include protected profiles including `Staging`, `Production`, and `ManagedApplicationReviewer`. |
| Port `AzureProviderStartupValidator` | Shared Api/Infrastructure-compatible namespace | Must fail closed before service resolution. |
| Wire validator in `Program.cs` | Startup fails before provider registrations if unsafe | No `Mock`, `InMemory`, `SQLite`, `DevelopmentAuth`, `SeedDemoData`, or fallback providers in protected runtimes. |

### 5.3 EF Core foundation migration

Use the existing `InsuranceIntelligenceDbContext` / design-time factory pattern where present. Do not invent a parallel DbContext unless the real repository has no usable EF foundation.

| Entity/Table | Required by | Source | Notes |
|---|---|---|---|
| `Tenant` | Tenant/workspace isolation | Database implementation plan | Root isolation table if not already present. |
| `Workspace` | Workspace scoping | Database implementation plan | Tenant-child workspace table. |
| `UserTenantMembership` | `EfPermissionReadRepository`, `AuthenticatedTenantResolver` | Core Ports | Maps external user identity to internal TenantId/WorkspaceId. |
| `RoleAssignment` | `EfPermissionReadRepository` | Core Ports | Links membership/user/workspace to role. |
| `Role` | Permission model | Core Ports + DB plan | Internal IIP role definition, not raw Entra role. |
| `RolePermission` | Permission model | Core Ports | Joins roles to fine-grained permission constants. |
| `Permission` | Permission model | Core Ports | Stores codes such as `claims.read`, `documents.review`, `approvals.review`. |
| `AuditEvent` | `EfAuditWriter`, `AuditQueryService` | Core Ports + Audit Query Service | Durable immutable audit record. |

Recommended migration naming:

```text
M001_FoundationTenancyAndPermissions
M002_AuditEvents
```

These can be split or combined according to the existing migration history, but the first usable repository milestone must include all core permission and audit tables.

### 5.4 Core port implementations

| Port | Concrete implementation | Source document | Notes |
|---|---|---|---|
| `ITenantContextAccessor` / `ITenantContextSetter` | `TenantContextAccessor` | Core Ports | Scoped request holder populated only after authenticated membership validation. |
| `IPermissionService` | `PermissionService` | Core Ports | Uses resolved `TenantContext` and `IPermissionReadRepository`. |
| `IPermissionReadRepository` | `EfPermissionReadRepository` | Core Ports | Queries `UserTenantMembership`, `RoleAssignment`, `Role`, `RolePermission`, `Permission`. |
| `IAuditWriter` | `EfAuditWriter` | Core Ports | Writes durable `AuditEvent`. |
| `IAuditEventRepository` | `EfAuditEventRepository` | Core Ports | Adds audit records to EF DbContext. |
| `IProviderHealthCheck` | `AzureProviderHealthCheck` | Core Ports | Dispatches provider codes to real connectivity checks. |
| `IProviderConnectivityCheck` | one implementation per adapter | Core Ports | Start with Blob Storage only as proof of pattern. |
| `AzureBlobStorageConnectivityCheck` | Blob connectivity wrapper | Core Ports | Reuse existing Blob provider if present. |

### 5.5 Foundation tests

| Test group | Must cover |
|---|---|
| Tenant context tests | Authenticated tenant resolution, workspace hint validation, no trust in client tenant headers. |
| Permission tests | Allowed/denied permission paths and store fallback when context permissions are empty. |
| Audit tests | Required audit fields, persistence, redaction/truncation. |
| Provider health tests | Not configured, unavailable, timeout, connected. |
| LocalTest isolation tests | Deployable projects do not reference or resolve LocalTest doubles. |
| Startup validator tests | Protected runtimes reject unsafe provider modes and seed/demo behavior. |

---

## 6. Per-service repository build-out

Each subsection lists the exact repository/provider ports the Application service depends on, the current implementation assumption, and the recommended EF persistence target. Status is **Not Started** until verified in the real repository.

### 6.1 Command Center

**Source document:** `iip_command_center_query_service_updated_logic.md`

- `CommandCenterQueryService` is the chat-first orchestration service, but it should not become a persistence dumping ground.
- Direct read tools should call the existing Application services once their repositories exist.
- Drafting relies on `INotificationDraftService`, which should persist drafts for later approval/send.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IIntentClassifier` | Deterministic-first classifier, optional AI fallback | No table | May use `IChatCompletionProvider` for fallback only. |
| `IProviderRequirementResolver` | Provider-code mapping service | No table | Uses canonical `ProviderCodes`. |
| `INotificationDraftService` | Concrete draft persistence service | `NotificationDrafts` | Also used by Notification Send phase. |
| `IChatCompletionProvider` / `IRetrievalProvider` / `IAiOutputValidator` | Real provider adapters or existing implementations to verify | AgentRun/AIInteraction if outputs are persisted | Verify against backend provider spec before adding tables. |

### 6.2 Claims Query

**Source document:** `iip_claims_query_service_implementation_output.md`

- This is the first full vertical slice after foundation.
- `IClaimsReadRepository` must serve list, detail, attention, daily summary, and draft-context queries without fabricating missing rows.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IClaimsReadRepository` | `EfClaimsReadRepository` | `Claims`, `ClaimTimelineItems`, `ClaimDocuments`, `ClaimCommunications`, `ClaimInternalNotes`, `ClaimAiBriefs` | Reuses `Customers` and `Policies` once those entities exist. |

### 6.3 SLA Risk

**Source document:** `iip_sla_risk_service_implementation_output.md`

- Second vertical slice after Claims because it reuses claim data and SLA fields.
- Avoid creating a separate SLA universe unless the source document requires it.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IClaimsSlaReadRepository` | `EfClaimsSlaReadRepository` | `Claims` with SLA fields; optional `Tasks`/`WorkItems` | Reuse Claim table/indexes from Claims phase. |
| `IClock` | System/fixed clock registration | No table | Shared utility also used by Reporting if same interface is retained. |

### 6.4 Approvals — Query and Decision

**Source document:** `iip_approval_query_service_implementation_output.md + iip_approval_decision_service_implementation_output.md`

- Approval query and decision should be implemented together because both depend on the `Approvals` table.
- Decision service must preserve `ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` as the non-bypassable human boundary.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IApprovalReadRepository` | `EfApprovalReadRepository` | `Approvals` plus related entity joins | Can join Claims/Documents/WorkflowCases as related entities. |
| `IApprovalDecisionRepository` | `EfApprovalDecisionRepository` | `Approvals` decision/status fields | Same table as query repository. |

### 6.5 Documents — Upload, Extraction, Review

**Source document:** `iip_document_upload_service_implementation_output.md + iip_document_extraction_workflow_service_implementation_output.md + iip_document_review_service_implementation_output.md`

- Document upload persists metadata and delegates binary storage to Blob provider adapter.
- Extraction workflow uses real Azure AI Document Intelligence adapter through the provider boundary.
- Review stores human corrections and field status changes.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IDocumentRepository` | `EfDocumentRepository` | `Documents` | Created before extraction/review. |
| `IDocumentUploadStorageProvider` | Blob-backed upload target provider | Blob metadata stored on `Documents` | Concrete Azure adapter, not EF. |
| `IDocumentExtractionProvider` | Azure Document Intelligence adapter | No table; output persisted via extraction repository | Provider health gated. |
| `IDocumentExtractionRepository` | `EfDocumentExtractionRepository` | `DocumentExtractions`, `ExtractedFields` | Uses `Documents` from upload phase. |
| `IDocumentReviewRepository` | `EfDocumentReviewRepository` | `ExtractedFields`, `HumanCorrections` or review fields | Reuses extraction tables. |

### 6.6 Completeness Check

**Source document:** `iip_completeness_check_service_implementation_output.md`

- Completeness is a rules/read-model layer over documents and claim type checklists.
- Do not duplicate the Documents table; store only checklist/rule definitions separately.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `ICompletenessRuleRepository` | `EfCompletenessRuleRepository` | `RequiredDocumentRules` / `DocumentChecklistRules` | One checklist table can support multiple claim types/product lines. |
| `IClaimDocumentReadRepository` | `EfClaimDocumentReadRepository` | `Documents` | Reuses Documents phase. |

### 6.7 Channels, Customers, Sales Leads

**Source document:** `iip_channels_query_service_implementation_output.md + iip_customers_query_service_implementation_output.md + iip_sales_leads_query_service_implementation_output.md`

- These are read-heavy query services and can be built in parallel after Claims/Documents foundation exists.
- Customer 360 should reuse Customers/Policies/Claims/Documents rather than creating duplicate customer context storage.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IChannelReadRepository` | `EfChannelReadRepository` | `ChannelConversations`, `ChannelMessages` / `Interactions` | Email and WhatsApp can later write into same conversation model. |
| `ICustomerReadRepository` | `EfCustomerReadRepository` | `Customers`, `Policies`, `Claims`, `Documents`, `Interactions` | Reuses Claims/Documents/Channels where available. |
| `ISalesLeadReadRepository` | `EfSalesLeadReadRepository` | `SalesLeads`, `SalesLeadRequiredInformation`, `SalesLeadFollowUpActions` | May later connect to customer/channel records. |

### 6.8 Audit Query

**Source document:** `iip_audit_query_service_implementation_output.md`

- Audit query is separate from `IAuditWriter`; foundation creates the write path, this phase adds query/read filtering.
- Do not add a second audit table.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IAuditReadRepository` | `EfAuditReadRepository` | `AuditEvent` | Reuses foundation audit table; indexes may need expansion. |

### 6.9 Claim Intake and Workflow Transition

**Source document:** `iip_claim_intake_workflow_transition_service.md`

- This phase creates real FNOL/claim creation and workflow state transition persistence.
- It should reuse Claims, Approvals, Documents, and Audit infrastructure instead of creating parallel workflow stores unless needed.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IClaimIntakeRepository` | `EfClaimIntakeRepository` | `Claims`, optional `FnolRecords`, `ClaimTimelineItems` | Creates the claim row used by later services. |
| `IClaimWorkflowRepository` | `EfClaimWorkflowRepository` | `Claims`, `ClaimWorkflowEvents` / `ClaimTimelineItems` | Updates status using specified transition graph. |
| `IClaimNumberGenerator` | SQL sequence/table-backed generator | `ClaimNumberSequences` or SQL sequence | Must be tenant/workspace aware. |
| `IClaimApprovalEvidenceRepository` | `EfClaimApprovalEvidenceRepository` | `Approvals` linked to `Claims` | Reuses Approvals phase. |

### 6.10 Integration Write-Back

**Source document:** `iip_integration_write_back_execution_service.md`

- This phase persists write-back history and defines the real provider boundary to core policy/admin systems.
- The external provider is insurer-specific and may be blocked until a customer system is known.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IPolicyAdminWriteBackProvider` | Concrete insurer-specific adapter | No generic table; adapter configuration may use `ConnectorConfigs` | Adapter deferred until integration target is chosen. |
| `IClaimWriteBackHistoryRepository` | `EfClaimWriteBackHistoryRepository` | `ClaimWriteBackHistory` / `WriteBackRequests` | Failure history must persist even when provider fails. |
| `IWriteBackApprovalReadRepository` | `EfWriteBackApprovalReadRepository` | `Approvals` | Reuses Approvals phase to verify human decision. |

### 6.11 Notifications

**Source document:** `iip_notification_send_execution_service.md + iip_email_ingestion_dispatch_provider_agnostic_port.md`

- Notification send uses approved persisted drafts and a real communication provider confirmation before marking sent.
- Email provider-agnostic ports may replace or wrap the older Graph-first path.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `INotificationDraftSendRepository` | `EfNotificationDraftSendRepository` | `NotificationDrafts`, `NotificationSendHistory` | Shares draft table with Command Center draft creation. |
| `ICommunicationSendingProvider` | Graph/email/SMS/WhatsApp sending adapter | No table; result stored in draft/send history | Can bridge to `IEmailOutboundProvider` for email. |

### 6.12 Knowledge Ingestion

**Source document:** `iip_knowledge_base_indexing_ingestion_service.md`

- Knowledge ingestion has both EF persistence and Azure AI adapter work.
- Raw knowledge content must not leak into audit summaries.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IKnowledgeDocumentRepository` | `EfKnowledgeDocumentRepository` | `KnowledgeDocuments`, `KnowledgeIngestionJobs`, optional `KnowledgeChunks` metadata | Blob source content and AI Search index are external resources. |
| `IKnowledgeSensitivityClassifier` | `KnowledgeSensitivityClassifier` | No table unless categories are tenant-configurable | Tenant Policy later references excluded categories. |
| `IKnowledgeChunker` | `KnowledgeChunker` | No table unless chunks persisted | May be pure Application logic. |
| `IEmbeddingProvider` | `AzureOpenAiEmbeddingProvider` | No table; vectors sent to AI Search | Azure adapter phase. |
| `IKnowledgeSearchIndexWriter` | `AzureAiSearchKnowledgeIndexWriter` | Azure AI Search index | Azure adapter phase. |

### 6.13 Reporting

**Source document:** `iip_reporting_service.md`

- Reporting aggregates over real repository data only; no estimated or smoothed figures.
- CSV can be implemented before PDF; PDF is blocked pending renderer/library decision.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IReportingReadRepository` | `EfReportingReadRepository` | Read aggregates over `Claims`, `Approvals`, `Documents`, SLA fields | Reuse prior tables; consider SQL views only after raw queries are proven. |
| `IReportSnapshotRepository` | `EfReportSnapshotRepository` | `ReportSnapshots`, `ReportExports` | Persists report snapshots and export status. |
| `IReportExportRenderer` | `CsvReportExportRenderer`; `PdfReportExportRenderer` later | No table | PDF blocked pending library decision. |
| `IReportExportArtifactStorageProvider` | Blob-backed report artifact storage | `ReportExports` stores artifact metadata | Reuse Blob provider; do not return temp-only artifacts. |

### 6.14 Email Ingestion and Dispatch

**Source document:** `iip_email_ingestion_dispatch_provider_agnostic_port.md`

- Email intake must be provider-agnostic and polling-correct by baseline.
- Push/webhook notification is a latency optimization for Graph/Gmail where available, not a correctness dependency.
- Attachments that become claim documents must route through `IDocumentUploadService`.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `IEmailInboundProvider` | Graph/Gmail/Zoho/IMAP adapter implementations | No common SQL table; checkpoints stored separately | Adapters deferred except Graph if already present. |
| `IEmailOutboundProvider` | Email-specific outbound adapter or bridge to `ICommunicationSendingProvider` | No table beyond Notification send history | Do not silently duplicate send pipeline. |
| `IEmailMailboxMappingRepository` | `EfEmailMailboxMappingRepository` | `EmailMailboxMappings` | Resolves mailbox to TenantId/WorkspaceId/system actor. |
| `IEmailInboundCheckpointRepository` | `EfEmailInboundCheckpointRepository` | `EmailInboundCheckpoints` | Provider-specific checkpoint serialized opaquely. |
| `IEmailInboundDispatchRepository` | `EfEmailInboundDispatchRepository` | `EmailInboundDispatchLog` / `EmailProcessingEvents` | Tracks idempotency and dispatch decisions. |

### 6.15 Tenant Agent Capability Policy

**Source document:** `iip_tenant_agent_capability_policy_structured_admin_configuration.md`

- Tenant policy is a structured configuration layer that can only narrow platform-wide AI capability.
- It must never bypass `ApprovalDecisionGuard`, audit writing, or human final-decision boundaries.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `ITenantAgentCapabilityPolicyRepository` | `EfTenantAgentCapabilityPolicyRepository` | `TenantAgentCapabilityPolicies` | Tenant/workspace-scoped policy row. |
| `ITenantAgentCapabilityPolicyService` | Application service implementation | `TenantAgentCapabilityPolicies` plus `AuditEvent` | Updates write `agent_policy_changed` audit events. |

### 6.16 Conversational Session State and Multi-Agent Scaffold

**Source document:** `iip_conversational_session_context_multi_agent_architecture.md`

- Session state stores operational continuity only; never raw evidence or retrieved claim/policy/document text.
- Multi-agent scaffold is future-state and should not block the 12 direct Application services.

| Port / dependency | Concrete implementation to build | Recommended entity/table | Reuse notes |
|---|---|---|---|
| `ICommandCenterSessionStateRepository` | `EfCommandCenterSessionStateRepository` | `CommandCenterSessionStates`, `CommandCenterTurnReferences`, `CommandCenterCompactedPointers` | Stores focus/reference pointers only. |
| `ICommandCenterSessionStateService` | Application service implementation | Uses session state tables | Compacts references at defined threshold. |
| `IAgentOrchestrator` | Future Microsoft Agent Framework-based orchestrator | `AgentRun`, `AIInteraction`, optional `AgentHandoffEvents` | Do not adopt before direct services are stable. |

---

## 7. Concrete Azure adapter build-out

These are not EF repositories. They are real provider adapters or renderer implementations required to make provider-gated capabilities work in protected runtimes.

| Adapter | Consuming service | Source document | Status / blocker |
|---|---|---|---|
| `AzureBlobStorageConnectivityCheck` | `AzureProviderHealthCheck` | Core Ports | Build first as provider-health proof. Reuse existing Blob provider if present. |
| Blob-backed `IDocumentUploadStorageProvider` | `DocumentUploadService` | Document Upload Service | Needed before real document upload works. |
| `AzureDocumentIntelligenceExtractionProvider` / `IDocumentExtractionProvider` | `DocumentExtractionWorkflowService` | Document Extraction Workflow Service + backend provider spec | Required for extraction; provider health-gated. |
| `AzureOpenAiChatCompletionProvider` / `IChatCompletionProvider` | Command Center AI fallback and agent synthesis | Backend provider spec + Command Center | Required for AI fallback; deterministic rules still run first. |
| `AzureAiSearchRetrievalProvider` / `IRetrievalProvider` | Knowledge-grounded answers and policy intelligence | Backend provider spec + Knowledge docs | Required for grounded retrieval. |
| `AzureOpenAiEmbeddingProvider` | `KnowledgeIngestionService` | Knowledge Ingestion | Required before indexing embeddings into AI Search. |
| `AzureAiSearchKnowledgeIndexWriter` | `KnowledgeIngestionService` | Knowledge Ingestion | Required to write searchable chunks to AI Search. |
| `GraphCommunicationSendingProvider` | `NotificationSendService` / `ICommunicationSendingProvider` | Notification Send | May be replaced or wrapped by provider-agnostic email outbound port. |
| Provider-agnostic email outbound bridge | `IEmailOutboundProvider` <-> `ICommunicationSendingProvider` | Email Ingestion + Notification Send | Define bridge before adding Gmail/Zoho/IMAP send adapters. |
| `CsvReportExportRenderer` | `ReportingService` | Reporting | Can be implemented immediately. |
| `PdfReportExportRenderer` | `ReportingService` | Reporting | Blocked pending PDF library/approach decision. |
| Gmail inbound/outbound adapter | `EmailToClaimIntakeDispatcher` / outbound email | Email Ingestion | Deferred; exact Gmail OAuth/API auth model TODO. |
| Zoho inbound/outbound adapter | `EmailToClaimIntakeDispatcher` / outbound email | Email Ingestion | Deferred; exact Zoho auth/API model TODO. |
| Generic IMAP/SMTP adapter | `EmailToClaimIntakeDispatcher` / outbound email | Email Ingestion | Deferred; polling baseline, IDLE reliability TODO. |
| Insurer-specific `IPolicyAdminWriteBackProvider` adapter | `PolicyAdminWriteBackService` | Integration Write-Back | Blocked until first customer core/policy system is selected. |

---

## 8. Recommended build order

| Phase | Name | Main deliverables | Reuse / dependency note |
|---:|---|---|---|
| 0 | Foundation | Application project, InfrastructureOptions, AzureProviderStartupValidator, EF core-port tables, core port implementations, Program.cs composition root | Blocks every other phase. |
| 1 | Claims | IClaimsQueryService + EfClaimsReadRepository + ClaimsController routes | First vertical slice and validation of tenancy/permissions/audit. Creates/reuses Claims, Customers, Policies, claim detail child tables. |
| 2 | SLA | ISlaRiskService + EfClaimsSlaReadRepository | Reuse Claims table/SLA fields; add indexes before separate tables. |
| 3 | Approvals | Approval query + decision repositories/services | Creates Approvals once; reused by write-back, notifications, claim workflow, tenant policy approval gates if added later. |
| 4 | Documents | Upload, extraction, review repositories and Blob/Document Intelligence adapters | Creates Documents and extraction tables; reused by completeness, claims detail, email attachments. |
| 5 | Completeness | Rules repository + claim document read repository | Reuse Documents; add only checklist/rule tables. |
| 6 | Channels / Customers / Sales Leads | Three read repositories; can run in parallel | Customer service reuses claims/documents/channels where possible. |
| 7 | Audit | IAuditReadRepository | Reuse foundation AuditEvent table; add read indexes/views if needed. |
| 8 | Claim Intake / Workflow | Intake repository, workflow repository, claim number generator, approval evidence repository | Creates claim rows that Claims/SLA/Documents can then display. |
| 9 | Write-Back | Write-back history repository + approval read repository + insurer adapter boundary | Reuse Approvals; add WriteBackRequests/ClaimWriteBackHistory. |
| 10 | Notifications | Draft send repository + communication sending provider | Reuse Approvals; add NotificationDrafts/SendHistory. |
| 11 | Knowledge | Knowledge repository + chunking + embeddings + AI Search writer | Uses Blob/AI Search/Azure OpenAI; policy restrictions later. |
| 12 | Reporting | Reporting aggregates, snapshots, export storage/renderers | Reuse Claims/Approvals/Documents/Audit; add ReportSnapshots/ReportExports only. |
| 13 | Email | Mailbox mappings, checkpoints, inbound dispatch, provider adapters | Reuse Claim Intake, Channels, Documents, Notifications. |
| 14 | Tenant Policy | TenantAgentCapabilityPolicy repository/service and enforcement checks | Add policy table; update Command Center, Notifications, Knowledge, Documents, Reporting checks. |
| 15 | Session State / Multi-Agent Scaffold | CommandCenterSessionState repository/service; future IAgentOrchestrator scaffold | Keep evidence out of session store; multi-agent only when direct services stabilize. |

---

## 9. Master status table

This is the working tracker the team should update as implementation proceeds. Keep the `Port/Adapter Name` and `Source Document` values stable unless the underlying source specification is explicitly revised.

| Port/Adapter Name | Consuming Service | Source Document | EF Entity/Table Needed | Status | Phase |
|---|---|---|---|---|---|
| `InfrastructureOptions` | Composition root / provider validation | `Core Ports + Backend Wiring + Delivery Tooling` | Configuration only; no table | Not Started | 0 - Foundation |
| `AzureProviderStartupValidator` | Composition root / protected runtime validation | `Core Ports + Backend Wiring + Delivery Tooling` | Configuration only; no table | Not Started | 0 - Foundation |
| `ITenantContextAccessor / ITenantContextSetter` | All Application services | `Core Ports` | No table; scoped request holder populated from UserTenantMembership | Not Started | 0 - Foundation |
| `IPermissionReadRepository -> EfPermissionReadRepository` | PermissionService / AuthenticatedTenantResolver | `Core Ports` | UserTenantMembership, RoleAssignment, Role, RolePermission, Permission | Not Started | 0 - Foundation |
| `IPermissionService -> PermissionService` | All permission-gated services | `Core Ports` | Uses permission tables above | Not Started | 0 - Foundation |
| `IAuditWriter -> EfAuditWriter` | All state-changing/sensitive-read services | `Core Ports` | AuditEvent | Not Started | 0 - Foundation |
| `IAuditEventRepository -> EfAuditEventRepository` | EfAuditWriter | `Core Ports` | AuditEvent | Not Started | 0 - Foundation |
| `IProviderHealthCheck -> AzureProviderHealthCheck` | Provider-gated services | `Core Ports` | Provider health may be runtime-only; optional ProviderStatus/IntegrationEvent for diagnostics | Not Started | 0 - Foundation |
| `IProviderConnectivityCheck` | AzureProviderHealthCheck | `Core Ports` | No core table; one implementation per provider adapter | Not Started | 0 - Foundation |
| `AzureBlobStorageConnectivityCheck` | IProviderHealthCheck proof adapter | `Core Ports` | No table; wraps Blob provider connectivity | Not Started | 0 - Foundation |
| `IProviderRequirementResolver` | CommandCenterQueryService | `iip_command_center_query_service_updated_logic.md` | No new table; maps intents/tools to ProviderCodes | Not Started | 1 - Command Center foundational wiring |
| `IIntentClassifier` | CommandCenterQueryService | `iip_command_center_query_service_updated_logic.md` | No table; deterministic rules plus optional AI fallback | Not Started | 1 - Command Center foundational wiring |
| `IChatCompletionProvider` | CommandCenterQueryService / AI fallback | `iip_backend_code_implementation_specification.md` | No table; provider adapter config | Not Started | 1 - Command Center foundational wiring |
| `IRetrievalProvider` | CommandCenterQueryService / knowledge-grounded answers | `iip_backend_code_implementation_specification.md` | Knowledge index external to SQL; may use KnowledgeDocuments/Chunks | Not Started | 1 - Command Center foundational wiring |
| `IAiOutputValidator` | CommandCenterQueryService / AI output safety | `iip_backend_code_implementation_specification.md` | No table unless validation logs are persisted through AgentRun/AIInteraction | Not Started | 1 - Command Center foundational wiring |
| `INotificationDraftService` | CommandCenterQueryService customer update draft path | `iip_command_center_query_service_updated_logic.md` | NotificationDrafts | Not Started | 12 - Notifications |
| `IClaimsReadRepository` | ClaimsQueryService | `iip_claims_query_service_implementation_output.md` | Claims, ClaimTimelineItems, ClaimDocuments, ClaimCommunications, ClaimInternalNotes, ClaimAiBriefs, Customers, Policies | Not Started | 1 - Claims |
| `IClaimsSlaReadRepository` | SlaRiskService | `iip_sla_risk_service_implementation_output.md` | Claims with SLAStatus/SlaDueAt/SlaBreachedAt; optionally Tasks/WorkItems | Not Started | 2 - SLA |
| `IClock` | SlaRiskService / ReportingService where specified | `iip_sla_risk_service_implementation_output.md + iip_reporting_service.md` | No table; runtime service | Not Started | 2 - SLA |
| `IApprovalReadRepository` | ApprovalQueryService | `iip_approval_query_service_implementation_output.md` | Approvals, related Claim/Document/WorkflowCase references | Not Started | 3 - Approvals |
| `IApprovalDecisionRepository` | ApprovalDecisionService | `iip_approval_decision_service_implementation_output.md` | Approvals with decision fields/status/decided-by/decided-at/reason | Not Started | 3 - Approvals |
| `IDocumentRepository` | DocumentUploadService | `iip_document_upload_service_implementation_output.md` | Documents | Not Started | 4 - Documents |
| `IDocumentUploadStorageProvider` | DocumentUploadService | `iip_document_upload_service_implementation_output.md` | No SQL table; Blob artifact target metadata stored on Documents | Not Started | 4 - Documents |
| `IDocumentExtractionProvider` | DocumentExtractionWorkflowService | `iip_document_extraction_workflow_service_implementation_output.md + backend provider spec` | No SQL table; Azure AI Document Intelligence adapter | Not Started | 4 - Documents |
| `IDocumentExtractionRepository` | DocumentExtractionWorkflowService | `iip_document_extraction_workflow_service_implementation_output.md` | Documents, DocumentExtractions, ExtractedFields | Not Started | 4 - Documents |
| `IDocumentReviewRepository` | DocumentReviewService | `iip_document_review_service_implementation_output.md` | DocumentExtractions, ExtractedFields, HumanCorrections | Not Started | 4 - Documents |
| `ICompletenessRuleRepository` | CompletenessCheckService | `iip_completeness_check_service_implementation_output.md` | RequiredDocumentRules / DocumentChecklistRules | Not Started | 5 - Completeness |
| `IClaimDocumentReadRepository` | CompletenessCheckService | `iip_completeness_check_service_implementation_output.md` | Documents filtered by ClaimId/TenantId/WorkspaceId | Not Started | 5 - Completeness |
| `IChannelReadRepository` | ChannelQueryService | `iip_channels_query_service_implementation_output.md` | ChannelConversations, ChannelMessages / Interactions | Not Started | 6 - Channels |
| `ICustomerReadRepository` | CustomerQueryService | `iip_customers_query_service_implementation_output.md` | Customers, Policies, Claims, Documents, Interactions | Not Started | 6 - Customers |
| `ISalesLeadReadRepository` | SalesLeadQueryService | `iip_sales_leads_query_service_implementation_output.md` | SalesLeads, SalesLeadRequiredInformation, SalesLeadFollowUpActions | Not Started | 6 - Sales Leads |
| `IAuditReadRepository` | AuditQueryService | `iip_audit_query_service_implementation_output.md` | AuditEvent, optionally AgentRun/AIInteraction for cross-reference | Not Started | 7 - Audit |
| `IClaimIntakeRepository` | ClaimIntakeService | `iip_claim_intake_workflow_transition_service.md` | Claims, ClaimTimelineItems/FNOL records, Customer/Policy lookups | Not Started | 8 - Claim Intake / Workflow |
| `IClaimWorkflowRepository` | ClaimWorkflowTransitionService | `iip_claim_intake_workflow_transition_service.md` | Claims, ClaimWorkflowEvents / ClaimTimelineItems | Not Started | 8 - Claim Intake / Workflow |
| `IClaimNumberGenerator` | ClaimIntakeService | `iip_claim_intake_workflow_transition_service.md` | ClaimNumberSequence or SQL sequence/table per tenant/workspace | Not Started | 8 - Claim Intake / Workflow |
| `IClaimApprovalEvidenceRepository` | ClaimWorkflowTransitionService | `iip_claim_intake_workflow_transition_service.md` | Approvals linked to ClaimId and approval evidence | Not Started | 8 - Claim Intake / Workflow |
| `IPolicyAdminWriteBackProvider` | PolicyAdminWriteBackService | `iip_integration_write_back_execution_service.md` | No generic SQL table; insurer-specific external adapter | Not Started | 9 - Write-Back |
| `IClaimWriteBackHistoryRepository` | PolicyAdminWriteBackService | `iip_integration_write_back_execution_service.md` | ClaimWriteBackHistory / WriteBackRequests | Not Started | 9 - Write-Back |
| `IWriteBackApprovalReadRepository` | PolicyAdminWriteBackService | `iip_integration_write_back_execution_service.md` | Approvals linked to write-back action/claim | Not Started | 9 - Write-Back |
| `INotificationDraftSendRepository` | NotificationSendService | `iip_notification_send_execution_service.md` | NotificationDrafts, NotificationSendHistory, linked ApprovalId | Not Started | 10 - Notifications |
| `ICommunicationSendingProvider` | NotificationSendService | `iip_notification_send_execution_service.md` | No SQL table; Graph/email/SMS/WhatsApp provider adapter | Not Started | 10 - Notifications |
| `IKnowledgeDocumentRepository` | KnowledgeIngestionService | `iip_knowledge_base_indexing_ingestion_service.md` | KnowledgeDocuments, KnowledgeIngestionJobs, KnowledgeChunks metadata | Not Started | 11 - Knowledge |
| `IKnowledgeSensitivityClassifier` | KnowledgeIngestionService | `iip_knowledge_base_indexing_ingestion_service.md` | No required table; policy/config may reference sensitivity categories | Not Started | 11 - Knowledge |
| `IKnowledgeChunker` | KnowledgeIngestionService | `iip_knowledge_base_indexing_ingestion_service.md` | No table unless chunk metadata persisted | Not Started | 11 - Knowledge |
| `IEmbeddingProvider` | KnowledgeIngestionService | `iip_knowledge_base_indexing_ingestion_service.md` | No SQL table; Azure OpenAI embedding adapter | Not Started | 11 - Knowledge |
| `IKnowledgeSearchIndexWriter` | KnowledgeIngestionService | `iip_knowledge_base_indexing_ingestion_service.md` | Azure AI Search index; optional KnowledgeIndexingJobs table | Not Started | 11 - Knowledge |
| `IReportingReadRepository` | ReportingService | `iip_reporting_service.md` | Aggregate queries over Claims, Approvals, Documents, SLA fields, Audit/reporting projections | Not Started | 12 - Reporting |
| `IReportSnapshotRepository` | ReportingService | `iip_reporting_service.md` | ReportSnapshots, ReportExports | Not Started | 12 - Reporting |
| `IReportExportRenderer` | ReportingService | `iip_reporting_service.md` | No table; CSV/PDF renderer implementation | Not Started | 12 - Reporting |
| `IReportExportArtifactStorageProvider` | ReportingService | `iip_reporting_service.md` | Blob artifact storage; ReportExports stores ArtifactUri/Size | Not Started | 12 - Reporting |
| `IEmailInboundProvider` | EmailToClaimIntakeDispatcher | `iip_email_ingestion_dispatch_provider_agnostic_port.md` | No common table; provider-specific checkpoint externalized via checkpoint repository | Not Started | 13 - Email |
| `IEmailOutboundProvider` | Email outbound adapter layer / ICommunicationSendingProvider bridge | `iip_email_ingestion_dispatch_provider_agnostic_port.md` | No table; sent status through NotificationDrafts/SendHistory if routed through notification service | Not Started | 13 - Email |
| `IEmailMailboxMappingRepository` | EmailToClaimIntakeDispatcher | `iip_email_ingestion_dispatch_provider_agnostic_port.md` | EmailMailboxMappings | Not Started | 13 - Email |
| `IEmailInboundCheckpointRepository` | EmailToClaimIntakeDispatcher | `iip_email_ingestion_dispatch_provider_agnostic_port.md` | EmailInboundCheckpoints | Not Started | 13 - Email |
| `IEmailInboundDispatchRepository` | EmailToClaimIntakeDispatcher | `iip_email_ingestion_dispatch_provider_agnostic_port.md` | EmailInboundDispatchLog / EmailProcessingEvents | Not Started | 13 - Email |
| `ITenantAgentCapabilityPolicyRepository` | TenantAgentCapabilityPolicyService | `iip_tenant_agent_capability_policy_structured_admin_configuration.md` | TenantAgentCapabilityPolicies | Not Started | 14 - Tenant Policy |
| `ICommandCenterSessionStateRepository` | CommandCenterSessionStateService | `iip_conversational_session_context_multi_agent_architecture.md` | CommandCenterSessionStates / CommandCenterTurnReferences / CompactedPointers | Not Started | 15 - Session State |
| `IAgentOrchestrator` | Future multi-agent scaffold | `iip_conversational_session_context_multi_agent_architecture.md` | AgentRun, AIInteraction, optional AgentHandoffEvents | Not Started | 15 - Session State / Multi-Agent scaffold |

---

## 10. Phase exit criteria

A phase is not complete merely because files exist. It is complete only when all of the following are true:

```text
- dotnet build passes for the whole solution.
- dotnet test passes for the relevant service and foundation tests.
- Tenant/workspace isolation tests exist for every repository in the phase.
- Permission-denial tests exist for every service in the phase.
- Audit-write tests exist for every state-changing or sensitive-read path.
- Protected runtime startup validation rejects unsafe provider modes.
- No deployable project references InsuranceIntelligence.LocalTestDoubles.
- No fake or fallback provider path exists in Api, Application, Infrastructure, or Functions.
- OpenAPI contract is updated if controller routes are added or changed.
- EF migration is generated, reviewed, and applied in a controlled environment.
```

---

## 11. Immediate next action

Start with **Phase 0 — Foundation**. The first engineering PR should not attempt multiple Application services. It should create the `InsuranceIntelligence.Application` project, port `InfrastructureOptions` and `AzureProviderStartupValidator`, implement the four core ports, create the EF foundation migration, and prove `IProviderHealthCheck` through Blob Storage connectivity. The next PR should implement **Phase 1 — Claims** as the first full vertical slice. The next service after Claims is **SLA Risk**, matching the established priority order.