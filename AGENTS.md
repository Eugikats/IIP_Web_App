# AGENTS.md — Insurance Intelligence Platform (IIP) Repository Contract

> **Canonical update — July 2026:** This file is aligned with the current IIP documentation set produced for the Insurance Intelligence Platform: Core Ports, Backend Wiring, InfrastructureOptions/Startup Validation, Application Services, Database/Repository Build-Out, Command Center Frontend Slice, AI Agent Architecture, Conversational Session/Context, Email Ingestion, Tenant Agent Capability Policy, Delivery Tooling, Managed Application Packaging, Entra multitenant identity onboarding, UI authority, and the zero-simulation production program.

---

## 0. Purpose and authority

You are an implementation agent working inside the repository for **Insurance Intelligence Platform (IIP)**.

This file is the repository-level build and release contract. Read it before making changes. Follow it unless a human maintainer explicitly updates it.

Before implementing anything, also read:

```text
IIP_IMPLEMENTATION_GUARDRAILS.md
CLAUDE.md
README.md
Relevant files under docs/
```

`AGENTS.md` is the highest-authority product, architecture, security, UI, deployment, and production release contract in the repository. If older prompts, older plans, mock-first MVP notes, legacy UI directions, or stale documents conflict with this file, follow this file and update the stale material.

IIP is not a generic chatbot and not a system that makes insurance decisions. It is a **chat-first, Azure-native, human-reviewed insurance operations platform** where AI prepares insurance work, retrieves evidence, summarizes, drafts, routes, flags issues, and prepares audit material, while humans remain accountable for all consequential insurance decisions.

---

## 1. Naming standard

Use these names consistently:

| Context | Required name |
|---|---|
| Formal product name | **Insurance Intelligence Platform (IIP)** |
| Short product name | **IIP** |
| Staff application name | **IIP Operations** |
| Repository/code identifiers | Keep existing names such as `InsuranceIntelligence.sln`, `InsuranceIntelligence.Api`, `InsuranceIntelligence.Application`, and `INSURANCEINTELLIGENCE_*` unless a maintainer approves a refactor. |

Do not use `ACIP`, `AIIP`, placeholder brand names, mock insurer names, or reference-image labels as the official product name.

---

## 2. Non-negotiable deployed-runtime rules

### 2.1 Zero simulation in deployed runtimes

No mock, simulated, fake, seeded-demo, placeholder, in-memory, SQLite, DevelopmentAuth, demo-header, hardcoded-insurer, hardcoded-user, fake-ingestion, fake-send, fake-dashboard, or fallback-to-mock behavior is permitted in:

```text
Staging
Production
ManagedApplicationReviewer
Customer-deployed Azure Managed Application instances
Partner Center/private-plan reviewer deployments
Any publicly reachable or insurer-facing environment
```

Test doubles may exist only in:

```text
Automated test projects
InsuranceIntelligence.LocalTestDoubles
InsuranceIntelligence.Api.LocalTestHost
```

The deployable API, Functions, frontend, and infrastructure packages must not reference or activate LocalTest doubles.

### 2.2 Runtime profiles

Use exactly these runtime profiles:

| Profile | Purpose | Test doubles allowed? | Deployable/customer-facing? |
|---|---|---:|---:|
| `LocalTest` | Unit/integration tests and isolated local verification | Yes | No |
| `Development` | Internal development only | Only if explicitly isolated | No |
| `Staging` | Production-candidate validation using synthetic records with real providers | No | Internal validation only |
| `Production` | Live insurer/customer operation | No | Yes |
| `ManagedApplicationReviewer` | Partner Center/private-plan review deployment | No | Yes |

Startup/readiness must fail closed in protected runtimes if any configured provider resolves to:

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

### 2.3 Empty data remains empty

Never fabricate operational records, dashboard metrics, provider health, claims, documents, approvals, communications, report rows, AI answers, or integration success. If data is absent, return an empty state. If a provider is absent, return `Not configured`. If a provider is unhealthy, return a safe provider error.

---

## 3. Product definition and deployment model

IIP is an Azure-native insurance operations platform for insurers, brokers, TPAs, HMOs, bancassurance teams, and related insurance operations teams.

It sits above existing core insurance systems and provides:

```text
Operational Command Center
Claims intelligence
Document intelligence
Policy and knowledge retrieval
Human approvals
Customer communication drafting
Audit and compliance evidence
Integration and write-back control
Reporting and management summaries
Agent-assisted insurance workflows
```

IIP does **not** replace core policy/claims administration systems. It orchestrates, enriches, retrieves, drafts, audits, and prepares actions that are reviewed by humans.

Commercial deployment priority is:

```text
Azure Managed Application
Partner Center private plan
Customer deploys into insurer Azure subscription/tenant
Publisher retains controlled managed-resource-group support/operations access
```

Default customer deployment is **one insurer per Managed Application instance**, with logical workspaces inside that insurer deployment for branches, departments, product lines, countries, or operational teams. The codebase must still preserve tenant/workspace scoping because it is required for SaaS readiness, internal isolation, auditability, and future deployment modes.

---

## 4. Human-supervised decision boundary

AI must never be the final actor for consequential insurance decisions.

Never implement autonomous final decisions for:

```text
Claim approval
Claim rejection or repudiation
Settlement approval
Payment release
Policy binding
Premium setting
Underwriting acceptance/rejection
Adverse underwriting outcome
Fraud determination or accusation
Complaint final response
Regulatory filing submission
Write-back execution without required approval
```

AI may:

```text
Summarize
Extract
Classify
Draft
Recommend
Route for review
Flag risk/fraud indicators without accusation
Prepare evidence packs
Prepare approval requests
```

Use `ApprovalDecisionGuard.EnsureHumanForConsequentialDecision` wherever applicable. No tenant configuration may bypass this platform ceiling.

---

## 5. Grounding, retrieval, and context rules

### 5.1 Retrieval-on-demand is the standing rule

AI must answer from authoritative, tenant/workspace-scoped data retrieved for the current turn:

```text
Claims records
Policy records
Approved knowledge-base content
Documents and extracted fields
Conversation/channel records
Approvals and workflow records
Provider health/status records
```

Do not answer policy, claims, coverage, compliance, payment, underwriting, or customer-impacting questions from model memory.

### 5.2 Session state is operational continuity only

`ICommandCenterSessionStateService` stores bounded operational continuity such as:

```text
Current claim/customer/document in focus
Active filters
Pending task references
Unresolved questions
Short pointers to prior workflow steps
```

It must never persist:

```text
Raw retrieved evidence
Claim/policy/document text
OCR text
Raw email bodies
Extracted document content
Prompt payloads
Long model responses
```

Long-running chat sessions must re-retrieve grounding data each turn. Foundry conversation state or compaction may be used only as short-horizon convenience, never as the evidentiary source or audit record.

### 5.3 Foundry usage boundary

Azure AI Foundry / Azure OpenAI is an inference/model-hosting layer. IIP Application services own:

```text
Retrieval policy
Evidence selection
Session state
Approval gates
Audit writing
Tenant/workspace enforcement
Multi-agent supervision
```

Foundry-side conversation compaction is opaque and must not be treated as source evidence, an audit trail, or permission state.

---

## 6. Canonical architecture

### 6.1 Backend stack

Use:

```text
.NET 8 / ASP.NET Core
Entity Framework Core
Azure SQL Database
Azure Blob Storage
Azure Service Bus
Azure AI Search
Azure AI Document Intelligence
Azure OpenAI / Azure AI Foundry-hosted models
Azure Key Vault
Application Insights / Log Analytics
Microsoft Entra ID
Managed Identity
Azure Container Apps as the default API hosting target for current IaC
Azure Functions / Durable Functions for background orchestration where needed
```

Avoid introducing AKS, Kafka, Redis, Cosmos DB, or a distributed microservice fleet unless there is a concrete approved need.

### 6.2 Required solution/project layout

Use and preserve this structure:

```text
src/
  InsuranceIntelligence.Api/
  InsuranceIntelligence.Application/
  InsuranceIntelligence.Domain/
  InsuranceIntelligence.Infrastructure/
  InsuranceIntelligence.Functions/
  InsuranceIntelligence.LocalTestDoubles/
  InsuranceIntelligence.Api.LocalTestHost/

tests/
  InsuranceIntelligence.Tests/
  InsuranceIntelligence.Tests.Integration/

web/
  portal/

infra/
  bicep/

docs/
```

Project-reference rules:

```text
Api -> Application, Infrastructure, Domain
Infrastructure -> Application, Domain
Application -> Domain
Domain -> no Application/API/Infrastructure dependency
LocalTestDoubles -> Application/Domain only where required
Api.LocalTestHost -> Api, Application, Infrastructure, LocalTestDoubles
Production Api/Functions must not reference LocalTestDoubles
```

### 6.3 Application layer is the business boundary

Application services enforce tenant/workspace scope, permissions, provider health, workflow rules, audit writing, and human-review boundaries. Infrastructure implements repositories and provider adapters only.

---

## 7. Core ports and infrastructure contract

The four core ports are mandatory and must not be bypassed:

```text
ITenantContextAccessor / ITenantContextSetter
IPermissionService / IPermissionReadRepository
IAuditWriter / IAuditEventRepository
IProviderHealthCheck
```

Rules:

```text
Every request resolves TenantContext server-side.
Every Application service checks permission and tenant/workspace scope.
Every state-changing or sensitive AI/workflow action writes audit.
Every provider-touching service checks provider health where specified.
No controller, frontend, route parameter, body, query string, or browser state is authority for tenant/workspace/role/permission.
```

Core infrastructure implementations:

```text
TenantContextAccessor
AuthenticatedTenantResolver
TenantWorkspaceContextMiddleware
PermissionService
EfPermissionReadRepository
EfAuditWriter
EfAuditEventRepository
AzureProviderHealthCheck
```

---

## 8. Authentication and tenant identity

### 8.1 Correct Entra model

IIP uses **one multitenant Microsoft Entra ID app registration** configured for:

```text
Accounts in any organizational directory
Authority: https://login.microsoftonline.com/organizations/v2.0
```

`InfrastructureOptions.EntraTenantId` represents IIP's publisher/home tenant where the app registration lives. It is **not** an insurer/customer tenant.

`EntraClientId` and `EntraAudience` represent the single IIP API/app registration and remain stable across insurer tenants.

### 8.2 Token claims are identity hints only

Use the validated Entra token for identity, not business authorization.

Preferred external user key:

```text
entra:{tid}:{oid}
```

Use `sub` only as a last-resort compatibility fallback when `tid` + `oid` are not available. Do not use email, UPN, preferred_username, or display name for authorization.

Runtime access flow:

```text
Validated JWT
-> extract tid/oid identity hint
-> AuthenticatedTenantResolver
-> IPermissionReadRepository.GetEffectiveMembershipAsync(userId, entraTenantHint, requestedWorkspaceId, ct)
-> TenantContext(TenantId, WorkspaceId, UserId, Roles, Permissions, CorrelationId)
-> controller/service execution
```

### 8.3 App roles and groups

Optional Entra app roles may be exposed only as coarse entry gates:

```text
IIP.Access
IIP.Admin
IIP.ReadOnly
```

They answer: **who may enter IIP at all?**

They do not grant fine-grained permissions such as:

```text
claims.read
documents.review
approvals.review
reports.export
agent_policy.manage
```

Fine-grained permissions remain in IIP's internal permission store resolved by `IPermissionReadRepository`.

Do not use raw group claims as the primary authorization mechanism. Group claims have enterprise-scale limits and cannot model IIP workspace/branch/product/assignment/approval semantics reliably.

### 8.4 Tenant onboarding identifiers

The onboarding portal must capture both:

| Identifier | Purpose |
|---|---|
| Entra tenant ID | Authentication/sign-in identity namespace; used as lookup input to IIP membership resolution. |
| Azure subscription ID(s) | Partner Center private-plan audience targeting and Managed Application deployment into the customer's subscription. |

These are separate fields. Do not assume one implies the other.

If a supplied subscription is a CSP-reseller subscription, flag it as a private-plan blocker before deployment planning continues.

### 8.5 SCIM

SCIM provisioning is phase two unless a launch customer contractually requires it. SCIM may create/deactivate users and group mappings in IIP tables, but runtime authorization still always goes through `IPermissionReadRepository`.

---

## 9. InfrastructureOptions and provider startup validation

`InfrastructureOptions` uses flat fields and provider option sub-records. Do not introduce competing nested configuration names.

Canonical mapping examples:

```text
Infrastructure:RuntimeProfile
Infrastructure:AuthMode
Infrastructure:StorageProvider
Infrastructure:BlobProvider
Infrastructure:DocumentProvider
Infrastructure:RetrievalProvider
Infrastructure:AiProvider
Infrastructure:QueueProvider
Infrastructure:EmailProvider
Infrastructure:CommunicationProvider
Infrastructure:NotificationProvider
Infrastructure:UseManagedIdentity
Infrastructure:SqlServerConnectionString
Infrastructure:EntraTenantId
Infrastructure:EntraClientId
Infrastructure:EntraAudience
Infrastructure:EntraIssuer
Infrastructure:AzureBlob:*
Infrastructure:AzureOpenAI:*
Infrastructure:AzureAiSearch:*
Infrastructure:DocumentIntelligence:*
Infrastructure:ServiceBus:*
```

`AzureProviderStartupValidator` must run before any service is resolved in protected runtimes.

Do not weaken fail-closed validation.

---

## 10. Application services and build order

Build thin vertical slices in this order:

```text
1. Foundation/core ports and DbContext
2. ClaimsQueryService
3. SlaRiskService
4. ApprovalQueryService + ApprovalDecisionService
5. DocumentUploadService + DocumentExtractionWorkflowService + DocumentReviewService
6. CompletenessCheckService
7. ChannelQueryService + CustomerQueryService + SalesLeadQueryService
8. AuditQueryService
9. ClaimIntakeService + ClaimWorkflowTransitionService
10. Integration Write-Back Execution Service
11. Notification Draft/Send services
12. Knowledge Ingestion/Indexing service
13. Reporting service
14. Email Ingestion/Dispatch provider-agnostic service
15. Tenant Agent Capability Policy service
16. Command Center Session State service
17. Optional Multi-Agent Orchestration scaffold
```

Do not jump ahead to later services before the foundation and earlier repository/entity dependencies compile and pass tests.

---

## 11. Repository and Azure adapter build-out expectations

Each Application repository/provider port must receive one real production implementation or be explicitly `Not configured` until implemented. Do not hide missing infrastructure behind mocks.

### 11.1 EF repositories

Use Azure SQL / EF Core for:

```text
Claims
SLA state/history
Approvals and decisions
Documents, extraction fields, corrections, status history
Required document rules
Channels/conversations
Customers
Sales leads
Audit query rows
Claim intake/workflow transition history
Write-back history
Notification drafts/sends
Knowledge document index status
Report snapshots/exports
Mailbox-to-tenant mappings and checkpoints
Tenant agent capability policy
Command Center session state
```

Every tenant-scoped entity must carry `TenantId` and `WorkspaceId` and be indexed accordingly.

### 11.2 Azure provider adapters

Build real adapters for:

```text
AzureBlobStorageProvider
AzureDocumentIntelligenceProvider
AzureAiSearchRetrievalProvider
AzureAiSearchKnowledgeIndexWriter
AzureOpenAiChatCompletionProvider
AzureOpenAiEmbeddingProvider
AzureServiceBusQueueProvider
GraphCommunicationSendingProvider or provider-agnostic replacement
Graph/Gmail/Zoho/ImapSmtp email adapters as configured
PDF report renderer after library decision
```

Provider-specific SDKs belong in Infrastructure. Application services depend on interfaces only.

---

## 12. Command Center contract

The Command Center is the primary interface of IIP Operations. It is **chat-first**, but not free-form chatbot-first.

Users ask operational questions like:

```text
Show me claims at SLA risk.
Draft a customer update for this claim.
Which claims are missing documents?
Explain why this claim is incomplete.
Prepare a management summary.
```

The API returns structured, grounded blocks such as:

```text
text
metric_pills
sla_summary
claims_table
document_gap_summary
evidence_list
action_proposal
not_configured
error
approval_queue
draft_communication
management_summary
```

The frontend must render backend DTO camelCase shapes exactly. Do not invent frontend-only block schemas.

Command Center layout:

```text
Left navigation
Central conversational workspace
Right Evidence/Context rail
Top bar with workspace/environment/user/role/alerts/search
```

The right rail displays evidence, current focus, sources, confidence where appropriate, provider status, and human-review boundaries.

---

## 13. Canonical AI agents

IIP has **10 canonical agents**:

| # | Agent | Responsibility | Final decision allowed? |
|---:|---|---|---:|
| 1 | Command Center Agent | Conversational operational briefing and structured command responses | No |
| 2 | Claims Summary Agent | Claim facts, timeline, issues, evidence summary | No |
| 3 | Document Checker Agent | Document classification, extraction review, gaps | No |
| 4 | Policy Intelligence Agent | Policy/coverage explanations from approved sources | No |
| 5 | Risk & Fraud Signal Agent | Risk/fraud indicators for human review, never accusation | No |
| 6 | Customer Update Drafting Agent | Customer-safe draft messages | No |
| 7 | Routing Agent | Assignment/escalation recommendations | No |
| 8 | Knowledge Assistant | Staff Q&A from approved knowledge | No |
| 9 | Report Insight Agent | Operational reporting summaries | No |
| 10 | Underwriting Intelligence Agent | Proposal/risk review support and referral notes | No |

If fraud becomes a separate specialist later, update the canonical architecture through an ADR before changing the count.

---

## 14. Multi-agent orchestration boundary

The current Application services work correctly as directly-called services. Do not introduce multi-agent orchestration prematurely.

When true cross-agent mid-flow correction is needed, use Microsoft Agent Framework SDK Sequential/Handoff patterns in the Application layer, with Foundry as inference-only.

The optional orchestrator shape is:

```text
Document Extraction Agent -> Completeness Check Agent -> Triage Agent -> Draft Agent
```

Rules:

```text
Raw document bytes/OCR text must not flow past Extraction.
Each step enforces its own permission.
Each step writes its own audit event.
Failures surface safely and stop downstream stale processing.
Field corrections re-trigger Completeness before Triage/Draft proceed.
No orchestrator step may call DecideAsync, SendApprovedDraftAsync, or ExecuteAsync for approval/notification/write-back services directly.
```

---

## 15. Email ingestion and communication providers

Email integration is provider-agnostic. Do not hardcode IIP to Microsoft Graph only.

Application ports:

```text
IEmailInboundProvider
IEmailOutboundProvider
```

Provider choices:

```text
Graph
Gmail
Zoho
ImapSmtp
```

Design baseline:

```text
Polling is the baseline guarantee.
Push/webhook is a latency optimization where the provider supports it.
Provider-specific IDs, delta tokens, folders, labels, and checkpoints remain adapter-internal.
```

Inbound email must resolve tenant/workspace through mailbox-to-tenant mapping. It must never bypass `IPermissionService`, `ITenantContextAccessor`, or `IAuditWriter` merely because it arrived asynchronously.

Attachments that become claim documents must go through `IDocumentUploadService`.

Outbound communication must preserve the existing draft/send pipeline:

```text
INotificationDraftService
INotificationSendService
ICommunicationSendingProvider / provider-agnostic outbound adapter
```

No simulated send in deployed runtime.

---

## 16. Tenant Agent Capability Policy

Tenants may configure AI capability policy only to **narrow** platform capability, never widen it.

Use explicit structured fields, not free-text AI-interpreted policy text.

Examples:

```text
AutoDraftCustomerCommunicationsEnabled
AutoSendRequiresApprovalAboveAmount
RequireTwoPersonApprovalAboveAmount
ManagementSummaryGenerationEnabled
DocumentAutoExtractionEnabled
KnowledgeRetrievalScopeRestriction
```

Required guard:

```text
TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling
```

A tenant cannot configure around:

```text
ApprovalDecisionGuard.EnsureHumanForConsequentialDecision
Audit writing
Server-side authorization
Human approval for consequential decisions
Zero-simulation rules
```

Default new-tenant policy must be conservative/restrictive.

---

## 17. Documents, knowledge, and reporting

### 17.1 Document processing

Documents must use:

```text
Azure Blob Storage for file storage
Azure AI Document Intelligence for deployed extraction
Azure SQL for document/extraction/correction/review state
Human correction and review workflows
Audit events for extraction, correction, status changes
```

### 17.2 Knowledge ingestion

Knowledge retrieval must use:

```text
Approved knowledge only
Tenant/workspace filters
Sensitivity labels
Approval status
Versioning
Index status and failure reason tracking
Azure AI Search
Azure OpenAI embeddings where configured
```

Do not make unapproved or sensitive knowledge retrievable by AI.

### 17.3 Reporting

Reports must be generated from real aggregates. No estimated or fabricated numbers.

Report exports require durable artifacts in Blob Storage, audit events, and safe failure handling. PDF renderer choice remains a business/technical decision until selected.

---

## 18. Database and schema rules

Use SQL Server/Azure SQL only for deployable runtime.

Do not use SQLite outside isolated local tests.

Schema must include domains for:

```text
Core ports and RBAC
Claims and FNOL
SLA and workflow transitions
Approvals and approval decisions
Documents and extraction fields
Channels/conversations
Customers and Customer 360
Sales leads
Audit
Write-back history
Notification drafts/sends
Knowledge indexing
Reporting snapshots/exports
Email mailbox mappings/checkpoints
Tenant agent capability policy
Command Center session state
```

Every tenant-scoped table must include:

```text
TenantId
WorkspaceId
Indexes on TenantId/WorkspaceId and primary lookup fields
```

Command Center session-state tables must not contain large raw text/evidence columns.

---

## 19. Frontend/UI authority

Before changing frontend page, layout, tokens, navigation, role behavior, Command Center, AI surface, customer-safe surface, approval flow, or backend mapping, read:

```text
docs/ui/IIP_Operations_UI_Design_Specification_MVP.md
docs/ui/reference/IIP_Global_Theme_Approved.png
docs/ui/backend-contract-map.md
Design System & UX Behavior Specification
UI/UX Handoff Document
Frontend Implementation Guide
Component Architecture Document
```

Approved visual direction:

```text
Light Intelligence Workspace theme
White/off-white/soft-lavender surfaces
Deep ink-navy typography
Subtle indigo/violet brand and AI accents
Light sidebar and light header
Structured right evidence/context rail
Compact operational tables
Subtle borders/shadows/radii
No heavy dark sidebar in MVP
No fake dashboard stats
```

Frontend must use real API contracts and real bearer-token auth. If token refresh is not complete, state it as a blocker; do not hardcode fake tokens.

---

## 20. API and backend route rules

The canonical route prefix is `/api/v1`. All product API routes are versioned; do not add new unversioned `/api/...` routes and do not reintroduce an unversioned form of an existing `/api/v1/...` route. Health endpoints (`/health`, `/health/live`, `/health/ready`) are the exception and remain outside `/api`.

Current routes include:

```text
POST /api/v1/command-center/query
GET /api/v1/command-center/metrics
GET /api/v1/system/capabilities
GET /api/v1/claims
GET /api/v1/claims/{claimId}
GET /api/v1/claims/attention
GET /api/v1/claims/daily-summary
GET /api/v1/claims/{claimId}/draft-context
GET /api/v1/approvals
GET /api/v1/approvals/{approvalId}
POST /api/v1/approvals/{approvalId}/approve
POST /api/v1/approvals/{approvalId}/reject
POST /api/v1/approvals/{approvalId}/escalate
POST /api/v1/approvals/{approvalId}/override
GET /api/v1/cases
POST /api/v1/cases
GET /api/v1/cases/{caseId}
GET /api/v1/cases/{caseId}/documents
POST /api/v1/cases/{caseId}/documents
GET /api/v1/cases/{caseId}/document-checklist
POST /api/v1/cases/{caseId}/documents/{documentId}/correct-field
POST /api/v1/cases/{caseId}/documents/{documentId}/reclassify
POST /api/v1/cases/{caseId}/documents/{documentId}/approve-extraction
GET /api/v1/cases/{caseId}/timeline
GET /api/v1/cases/{caseId}/recommended-actions
POST /api/v1/cases/{caseId}/workflow/evaluate
GET /api/v1/knowledge-documents
POST /api/v1/knowledge-documents
POST /api/v1/knowledge-documents/{knowledgeDocId}/approve
GET /api/v1/customers
GET /api/v1/customers/{customerId}/360
GET /api/v1/distribution/branches
GET /api/v1/distribution/producers
GET /api/v1/policies
GET /api/v1/policies/{policyId}/coverage
GET /api/v1/compliance/audit-events
GET /api/v1/compliance/cases/{caseId}/audit-pack
GET /api/v1/assistant/actions
POST /api/v1/assistant/policy-questions
```

There is no backend Channels or Sales & Leads API yet. The Track B portal pages for those areas (`/channels`, `/sales-leads`) are UI scaffolding without a corresponding backend endpoint — do not reference `/api/v1/channels/...` or `/api/v1/sales-leads/...` as if they exist until those services are built.

The prose Command Center assistant path (`POST .../command-center/assistant`) and the multi-agent query path (`POST /api/v1/assistant/query`) were removed in the v2 migration; the chat-first `POST /api/v1/command-center/query` and the grounded `POST /api/v1/assistant/policy-questions` are the current AI-answer endpoints. Do not reintroduce the removed paths.

Controllers are thin adapters. Business rules stay in Application services.

---

## 21. Azure Managed Application and Partner Center packaging

Near-term commercial path:

```text
Azure Managed Application
Partner Center private plan
Customer deploys into insurer Azure subscription/tenant
Publisher retains managed resource group support/operations access
```

### 21.1 Bicep and package path

Standard engineering Bicep remains under:

```text
infra/bicep/main.bicep
infra/bicep/modules/*
```

Managed Application packaging adds:

```text
infra/bicep/marketplace/main.managedapp.bicep
infra/bicep/marketplace/createUiDefinition.json
infra/bicep/marketplace/main.managedapp.production.bicepparam
scripts/marketplace/compile-managedapp-template.ps1|.sh
scripts/marketplace/build-managedapp-package.ps1|.sh
scripts/marketplace/validate-arm-language-version.ps1|.sh
```

Bicep is authoring-only for marketplace packaging. Partner Center receives a ZIP with ARM JSON artifacts at package root:

```text
mainTemplate.json
createUiDefinition.json
viewDefinition.json   # if used
```

### 21.2 Managed Application gates

Managed Application package must validate:

```text
Compiled ARM languageVersion remains compatible with current Managed Application requirements.
ManagedApplicationReviewer profile is used for reviewer deployments.
Publisher Management Access parameters are correct before plan goes live.
Customer Access / deny-assignment posture is deliberate.
JIT access principals are individually included where required.
No mock provider choices are exposed in createUiDefinition.
```

Publisher Management Access and customer access/deny-assignment posture are treated as plan-level one-way-door decisions once the plan goes live. Verify them with current Microsoft/Partner Center documentation before committing a submission date.

Managed Application RBAC/deny assignments are control-plane controls. Data-plane access to Azure SQL, Blob, Key Vault, AI Search, Azure OpenAI, Document Intelligence, and Service Bus must still use managed identity/RBAC/credentials as designed.

---

## 22. Security and customer-safe boundaries

Never expose to unauthorized users or customer-facing surfaces:

```text
Internal notes
Risk/fraud flags
Claim reserves
Prompt/system messages
Raw provider errors
Tokens/secrets/connection strings/SAS URLs
Raw email bodies unless authorized
Sensitive audit details
Other tenant data
Unapproved knowledge
Model/tool traces
```

Render untrusted content safely. Do not use unsafe HTML insertion for email, OCR, document text, customer text, or AI output.

---

## 23. Audit and telemetry

Every agent-assisted, workflow-changing, provider-touching, approval-related, communication-related, ingestion, indexing, export, or security-sensitive action must write an audit event.

Audit records must include where applicable:

```text
TenantId
WorkspaceId
Actor type and actor ID
Action/event type
Target type and target ID
Before/after safe values
Evidence/source references
CorrelationId
Provider/model metadata where AI/provider used
Human review status
Timestamp
```

Do not put secrets, raw prompts, raw tokens, full provider payloads, or excessive sensitive content into audit rows.

---

## 24. CI/CD and validation gates

Minimum validation commands:

```bash
# backend
dotnet restore InsuranceIntelligence.sln
dotnet build InsuranceIntelligence.sln --no-restore
dotnet test InsuranceIntelligence.sln
dotnet list InsuranceIntelligence.sln package --vulnerable --include-transitive

# portal
cd web/portal
pnpm install --frozen-lockfile
pnpm lint
pnpm test
pnpm build
cd ../..

# infrastructure
az bicep build --file infra/bicep/main.bicep
az deployment group validate --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.staging.bicepparam
az deployment group what-if --resource-group <rg> --template-file infra/bicep/main.bicep --parameters infra/bicep/main.staging.bicepparam
```

Managed Application package validation must include:

```text
compile main.managedapp.bicep to mainTemplate.json
validate ARM template languageVersion compatibility
zip mainTemplate.json + createUiDefinition.json at root
validate no fake provider parameters are exposed
validate ManagedApplicationReviewer runtime profile
```

Never claim build/test/deploy success unless commands actually ran and output is available. If a tool is unavailable, state that plainly.

---

## 25. Production Release Program

Execute gates in order:

```text
Gate 0 — Secure baseline, UI/backend alignment, real API contract map
Gate 1 — Real identity, authorization, config validation, Key Vault/secrets
Gate 2 — Real Azure staging / production-candidate deployment
Gate 3 — Activate and prove all real providers, including email
Gate 4 — Observability, reliability, backup/restore, CI/CD, security hardening
Gate 5 — End-to-end production-candidate acceptance
Gate 6 — Azure Managed Application package and Partner Center private-plan readiness
```

Do not skip gates to demonstrate faster.

Production-candidate requires live Azure staging with synthetic records and real providers. Partner Center/private-plan ready requires package validation, reviewer deployment validation, and readiness evidence.

---

## 26. Documentation and ADR discipline

Use docs as source of truth. When implementation changes behavior, update the relevant document or add an ADR.

Required ADR areas:

```text
Runtime baseline and hosting
Human-supervised decision boundary
Tenant isolation model
RAG grounding and evidence
Managed Application packaging
Email provider model
Role-based dashboard boundaries
Multitenant Entra identity model
Conversational session/context strategy
Tenant agent capability policy
Multi-agent orchestration boundary
API route versioning, if /api/v1 is introduced later
```

Do not bury architecture changes in code without documentation.

---

## 27. Coding agent working behavior

When asked to implement:

1. Inspect the existing repository first.
2. Read this file and the relevant spec documents.
3. Do not create competing architecture if an agreed one exists.
4. Prefer small vertical slices.
5. Keep Application business logic independent from Azure SDKs.
6. Put provider-specific SDK code in Infrastructure adapters.
7. Preserve tenant/workspace scoping in every query.
8. Preserve human-review boundaries.
9. Preserve zero-simulation rules.
10. Add or update tests with every non-trivial change.
11. Run relevant build/test/lint/infra checks.
12. Report exact files changed and exact command results.
13. State blockers honestly.
14. Do not claim production readiness without gate evidence.

Completion report must include:

```text
Files created/modified
Routes/contracts affected
Security impact
Tenant isolation impact
AI/human-review impact
Tests added/run and real output
What is real vs stubbed/not configured
Manual Azure/Microsoft 365/Partner Center actions required
Next recommended vertical slice
```

---

## 28. Current top priorities

The current implementation priority is:

```text
1. Foundation layer: Application project, core ports, DbContext, InfrastructureOptions, AzureProviderStartupValidator.
2. ClaimsQueryService vertical slice.
3. SLA risk service.
4. Approvals.
5. Documents and completeness.
6. Command Center frontend vertical slice against real API.
7. Managed Application/private-plan packaging track in parallel.
8. Multitenant Entra onboarding/admin-consent flow.
```

Do not spend engineering time on optional voice, advanced fraud modules, public marketplace listing, AKS, Fabric, Purview, or full multi-agent autonomy until the foundation, claims/document workflow, real staging, and managed-app private-plan path are stable.

---

## 29. Final reminder

IIP wins by being safe, grounded, auditable, tenant-isolated, and deployable for real insurers.

For deployed environments:

```text
No simulation.
No mock runtime providers.
No fake success paths.
No hardcoded insurer data.
No AI final decisions.
No ungrounded policy/claim answers.
No frontend-only authority.
No production-readiness claims without evidence.
```

Build the real platform, one verified vertical slice at a time.
