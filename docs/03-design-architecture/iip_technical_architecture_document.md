# Insurance Intelligence Platform (IIP) - Technical Architecture Document

**Document type:** Technical Architecture Document / Technical Specification Document (TAD/TSD)  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Engineering, Cloud, Security, AI, QA, and Delivery Teams  
**Date:** 30 June 2026  
**Version:** 1.0 - Consolidated MVP Technical Architecture  
**Status:** Working technical baseline for MVP implementation  
**Classification:** Internal - Product, Engineering, Architecture, Cloud, Security, AI, Delivery, and Leadership Reference

---

## Document Control

| Field | Value |
|---|---|
| Owner | Architecture / Engineering Lead |
| Primary audience | Engineering, architecture, cloud, security, AI, QA, DevOps, delivery, product leadership |
| Source baseline | IIP Master Product Context, IIP PRD, IIP FSD, Antigravity/codebase findings, UI/UX notes, meeting notes, lifecycle checklist |
| Current product name | **Insurance Intelligence Platform (IIP)** |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| Architecture stance | Azure-first, human-reviewed, auditable, tenant-aware, integration-ready |
| Decision model | AI assists. Humans make regulated insurance decisions. |

---

## 1. Executive Summary


---

# July 2026 Update: Chat-First Command Center Architecture

## Architecture Decision

The Command Center must be implemented as a **chat-first operational command workspace** backed by real APIs, scoped agent tools, typed response blocks, retrieval evidence, and auditable agent runs.

The central chat interface is the main experience layer for operational questions. It must not call Azure OpenAI directly from the browser and must not fabricate operational metrics in the frontend.

## Target Logical Flow

```text
IIP Operations user
  -> Command Center Chat Workspace
  -> POST /api/v1/command-center/query
  -> Command Center Orchestrator
  -> Scoped Tool Registry
  -> Claims / SLA / Documents / Approvals / Reports / Provider Health services
  -> Azure AI Search for approved knowledge and evidence retrieval
  -> Azure OpenAI / Foundry for grounded synthesis where needed
  -> Typed command response blocks
  -> AuditEvent + AgentRun + Evidence references
  -> UI renders chat message + embedded operational cards + evidence rail
```

## Required Backend Components

| Component | Responsibility |
|---|---|
| `CommandCenterOrchestrator` | Accepts user command, resolves intent, calls scoped tools, composes structured response blocks. |
| `CommandCenterBriefingService` | Produces daily operational briefing using real tenant/workspace data. |
| `CommandCenterToolRegistry` | Exposes only safe, typed tools for claims, SLA, documents, approvals, channels, reports, and provider health. |
| `CommandResponseBlockRendererContract` | Defines response block types that the frontend can render safely. |
| `EvidenceResolutionService` | Links answers to claims, documents, policies, knowledge chunks, metrics, and audit evidence. |
| `CommandCenterAuditWriter` | Logs user command, tools used, evidence IDs, generated output, actions proposed, and human decisions. |

## Required Frontend Components

| Component | Responsibility |
|---|---|
| `CommandChatWorkspace` | Main center column and conversation thread. |
| `CommandComposer` | Sticky input for operational commands. |
| `MetricPillStrip` | Compact metric pills embedded in briefing or response messages. |
| `CommandResponseBlock` | Switch renderer for structured blocks. |
| `ClaimsResultBlock` | Claims table/card block inside a chat response. |
| `SlaRiskBlock` | SLA risk summary and linked claims. |
| `ActionProposalBlock` | Human-reviewable proposed next actions. |
| `ContextEvidenceRail` | Right panel showing current focus, sources, confidence, selected record, and human responsibility. |

## Typed Response Block Contract

```ts
type CommandCenterResponseBlock =
  | { type: 'text'; markdown: string }
  | { type: 'metric_pills'; items: MetricPill[] }
  | { type: 'claims_table'; rows: ClaimAttentionRow[]; filters?: AppliedFilter[] }
  | { type: 'sla_summary'; items: SlaRiskItem[] }
  | { type: 'document_gap_summary'; items: MissingDocumentItem[] }
  | { type: 'approval_queue'; items: ApprovalQueueItem[] }
  | { type: 'action_proposal'; actions: ProposedHumanAction[] }
  | { type: 'evidence_list'; sources: EvidenceSource[] }
  | { type: 'not_configured'; provider: string; adminAction: string }
  | { type: 'error'; code: string; safeMessage: string; correlationId: string };
```

## Architectural Guardrails

- The frontend must render only backend-provided operational values.
- The command agent must use tenant/workspace-scoped tools only.
- No generic SQL tool may be exposed to the agent.
- No agent tool may approve/reject a claim, release payment, bind policy, accuse fraud, or issue final regulated communication.
- Missing providers must return `not_configured` blocks, not fake success.
- Every command query that uses AI or operational tools must create an `AgentRun` and related `AuditEvent` where applicable.

## Phase 1-4 Technical Alignment

| Phase | Technical Work |
|---|---|
| Phase 1 | Build chat workspace layout and typed frontend block renderer with mocked local fixtures only in isolated `LocalTest`. |
| Phase 2 | Implement real briefing, metrics, claims attention, SLA risk, provider health, and evidence endpoints. |
| Phase 3 | Implement orchestrated command query flow with scoped tools, RAG, action proposals, and human review gates. |
| Phase 4 | Harden auth, audit, observability, provider validation, security tests, UI QA, and no-simulation deployment checks. |
The **Insurance Intelligence Platform (IIP)** is an Azure-native insurance operations platform that helps insurers process claims, review documents, manage customer interactions, support acquisition workflows, and maintain auditable human-reviewed AI workflows.

The first technical baseline supports a **Motor Claims and Document Intelligence MVP** for a mid-to-large insurance company pilot. The MVP must receive customer and broker inputs from WhatsApp, email, web forms, call center notes, broker submissions, and internal uploads; convert them into structured work items; process documents with Azure AI Document Intelligence; retrieve approved knowledge through Azure AI Search; generate evidence-linked AI summaries through Azure OpenAI; route work to human reviewers; maintain audit trails; and export or hand off reviewed claim packs to downstream insurer systems.

The architecture must be designed around one non-negotiable principle:

> **AI prepares insurance work. Humans make regulated insurance decisions.**

IIP must not autonomously approve claims, reject claims, bind policies, set final premiums, accuse customers of fraud, release payments, or make final underwriting decisions. The system must support AI-assisted preparation, extraction, classification, summarization, routing, drafting, and advisory risk signaling, while ensuring human review and auditability for consequential insurance actions.

This document defines the target architecture, Azure service map, deployment model, environment strategy, frontend/backend structure, AI and agent architecture, data architecture, integration architecture, security model, observability model, resilience approach, DevOps approach, non-functional requirements, risks, and implementation sequence.

---

## 2. Purpose of this Document

This Technical Architecture Document defines how IIP should be built, deployed, secured, integrated, monitored, and operated.

It answers the following technical questions:

1. What is the target Azure architecture for IIP?
2. What services, components, and data stores are required for the MVP?
3. How do frontend, backend, AI, document intelligence, integrations, and workflows interact?
4. How should tenant, workspace, role, and permission boundaries be enforced?
5. How should AI actions be grounded, audited, and controlled?
6. How should insurer data be stored, indexed, processed, exported, and retained?
7. How should IIP integrate with WhatsApp, Microsoft Graph, email, policy systems, claims systems, files, and APIs?
8. How should deployment work across dev, staging, production, SaaS, private deployment, and Azure Managed Application scenarios?
9. How should the platform handle observability, incident response, disaster recovery, and operational support?
10. What technical risks, assumptions, and open decisions remain?

This document is not a full low-level implementation guide. It does not replace database migration scripts, OpenAPI contracts, IaC templates, or source-code design documents. Those should be produced as follow-on implementation artifacts.

---

## 3. Architecture Scope

### 3.1 MVP Technical Scope

The MVP architecture must support:

- Internal insurer staff authentication and authorization.
- Tenant and workspace context.
- Role-aware navigation and permission checks.
- Command Center dashboard and operational summary.
- Channels Inbox for WhatsApp, email, SMS/webchat/future channels.
- Customers / Customer 360 internal workspace.
- Sales and Leads internal workspace.
- Claims Work Queue.
- FNOL / New Claim Intake.
- Motor Claim Case Review.
- Document upload, storage, classification, OCR, extraction, and validation.
- Missing-document checklist.
- AI claim summary generation.
- Advisory fraud-signal flags.
- Human review and approvals.
- Customer-safe update drafting.
- Audit and compliance logs.
- Export of claim packs and structured data.
- Provider health, integration health, and operational monitoring.

### 3.2 MVP Out of Scope

The MVP must not include:

- Autonomous claim approval.
- Autonomous claim rejection.
- Automatic settlement.
- Final premium calculation.
- Final underwriting acceptance or rejection.
- Policy binding.
- Payment release automation.
- Full voice/call-agent implementation.
- Full Microsoft Marketplace transactable SaaS unless separately prioritized.
- Full offline platform mode.
- Deep custom integration into every insurer core system.
- Cross-country regulatory automation.
- Advanced Microsoft Fabric analytics unless separately scoped.
- Full fraud investigation automation.

### 3.3 Roadmap Technical Scope

The architecture should be extensible for:

- Customer Acquisition module.
- Fraud Intelligence module.
- Early Warning / Retention module.
- Underwriting Support module.
- Policy Servicing module.
- Broker workspace.
- Complaint management.
- Advanced analytics and Power BI / Fabric dashboards.
- Azure Managed Application packaging.
- Enterprise private deployments.
- Resilient channel buffering for unreliable connectivity.
- Future voice/call agent.

---

## 4. Architecture Principles

| Principle | Architecture Implication |
|---|---|
| Azure-first | Use Azure-native services by default unless a clear exception exists. |
| Human-reviewed | Consequential insurance actions require human approval workflows. |
| Evidence-linked | AI outputs must link to source documents, extracted fields, records, or knowledge references. |
| Tenant-aware | Every claim, customer, policy, document, audit event, lead, interaction, and AI run must carry tenant and workspace context. |
| Least privilege | Users, services, agents, and integrations receive only the minimum access required. |
| No frontend-only authorization | Every sensitive backend endpoint must enforce server-side authorization. |
| No uncontrolled database tools | AI agents must not receive generic SQL/database access. Only scoped tools are allowed. |
| No production mock data | Staging and production must call real configured services or show Not Configured / error states. |
| Audit-first | Important user, AI, document, integration, and workflow actions must be logged. |
| Configurable workflows | Insurer, claim type, document checklist, approval thresholds, and SLA rules should be configurable rather than hardcoded. |
| Graceful degradation | If a provider is unavailable, the UI should show status, queue work where safe, and avoid silent failure. |
| Integration-ready | MVP should support APIs, files, email, Microsoft Graph, and future policy/claims system integration. |

---

## 5. Target Logical Architecture

### 5.1 High-Level Architecture

```text
External Channels
WhatsApp | Email | Web Forms | Broker Uploads | Call Center | Future SMS/WebChat
        |
        v
Channel & Integration Layer
Webhook Receivers | Microsoft Graph | File Upload | API Gateway | Event Queue
        |
        v
IIP Core Application Layer
Next.js Frontend | .NET 8 API | Workflow Services | Agent Tool Registry
        |
        v
Insurance Work Objects
Customers | Policies | Claims | Documents | Leads | Conversations | Tasks | Approvals
        |
        v
AI & Knowledge Layer
Azure Document Intelligence | Azure AI Search | Azure OpenAI | Prompt/Tool Governance
        |
        v
Human Review & Audit Layer
Review Cockpit | Approvals | Audit Events | Evidence Packs | Reports
        |
        v
Downstream / External Systems
Policy Admin | Claims System | CRM | SharePoint | Email | Power BI | CSV/API Export
```

### 5.2 Layered Architecture

| Layer | Responsibility |
|---|---|
| Experience layer | Web app, internal workspaces, dashboards, role-based navigation, copilot panels. |
| API layer | Authenticated backend endpoints, business APIs, validation, authorization, data access. |
| Workflow layer | Long-running claim/document/channel workflows, retries, human review pauses, escalations. |
| AI service layer | Summarization, extraction support, classification support, policy Q&A, customer-safe drafting. |
| Knowledge layer | Approved policy documents, SOPs, checklists, indexed evidence, RAG retrieval. |
| Data layer | Transactional data, documents, audit logs, AI runs, integration events, workflow state. |
| Integration layer | WhatsApp, Microsoft Graph, APIs, files, future policy/claims system connectors. |
| Security layer | Identity, RBAC/ABAC, tenant isolation, secrets, encryption, audit, policies. |
| Observability layer | Application telemetry, logs, metrics, traces, alerts, health checks, incident support. |
| Deployment layer | IaC, environments, CI/CD, Azure resources, managed/private deployment packaging. |

---

## 6. Azure Service Architecture

### 6.1 Core Azure Service Map

| Platform Capability | Recommended Azure Service | MVP Role |
|---|---|---|
| Frontend hosting | Azure App Service, Azure Static Web Apps, or Azure Container Apps | Host Next.js app. |
| Backend API hosting | Azure App Service or Azure Container Apps | Host .NET 8 API. |
| Background workflows | Azure Functions / Durable Functions | Document processing, ingestion, retries, escalations. |
| API gateway | Azure API Management | Secure and govern external APIs when required. |
| Transactional database | Azure SQL Database | Claims, customers, tasks, leads, users, config, audit metadata. |
| Document storage | Azure Blob Storage | Uploaded claim files, policy documents, evidence packs, exports. |
| Queueing / async events | Azure Service Bus | Document processing queues, channel events, workflow events. |
| Event routing | Azure Event Grid | Optional event notification and integration events. |
| AI reasoning | Azure OpenAI | Summaries, drafting, Q&A, structured output generation. |
| Document OCR/extraction | Azure AI Document Intelligence | OCR, classification support, field extraction. |
| Knowledge retrieval | Azure AI Search | Policy/SOP/checklist/document retrieval and RAG. |
| Secrets | Azure Key Vault | Connection strings, API keys, certificates, client secrets. |
| Identity | Microsoft Entra ID | Staff SSO, app registration, RBAC foundations. |
| Observability | Azure Monitor, Application Insights, Log Analytics | Metrics, logs, traces, dashboards, alerts. |
| Security operations | Microsoft Sentinel / Defender for Cloud | SIEM and cloud security posture for enterprise deployments. |
| Analytics | Power BI / Microsoft Fabric later | Executive dashboards and advanced analytics. |
| IaC | Bicep or Terraform | Repeatable deployments. |
| CI/CD | GitHub Actions or Azure DevOps | Build, test, deploy, release control. |

### 6.2 Region Strategy

| Region | Role |
|---|---|
| South Africa North | Preferred Africa-proximity region for pilot and African customers, pending service quota confirmation. |
| West Europe | Fallback if specific Azure OpenAI models, quotas, or AI services are unavailable in South Africa North. |

Region and model availability must be verified before production. This document does not constitute legal approval for cross-border processing.

### 6.3 Azure Resource Naming Convention

A consistent naming convention should be used:

```text
otic-iip-{env}-{region}-{resource}-{suffix}
```

Examples:

```text
otic-iip-dev-zan-rg-core
otic-iip-stg-zan-app-api
otic-iip-stg-zan-app-web
otic-iip-prod-zan-sql-core
otic-iip-prod-zan-st-docs
otic-iip-prod-zan-kv-core
otic-iip-prod-zan-sbus-core
otic-iip-prod-zan-aisearch-core
```

Where:

| Token | Meaning |
|---|---|
| `otic` | Company / publisher prefix. |
| `iip` | Product acronym. |
| `env` | dev, test, stg, prod. |
| `region` | region abbreviation, e.g. zan for South Africa North. |
| `resource` | resource type. |
| `suffix` | logical purpose. |

---

## 7. Deployment Models

### 7.1 Supported Deployment Options

| Model | Description | Use Case |
|---|---|---|
| Otic-hosted SaaS | IIP runs in Otic-managed Azure environment with logical tenant isolation. | Fast demos, small insurers, brokers, early pilots. |
| Enterprise private deployment | IIP runs in a dedicated Azure subscription or customer-controlled Azure tenant. | Larger insurers, banks, HMOs, regulated customers. |
| Azure Managed Application | IIP is packaged for controlled deployment into customer Azure subscription with managed support access. | Marketplace/private offer path. |
| Hybrid control plane/data plane | Otic operates licensing, telemetry, support, product updates; insurer data plane remains isolated. | Long-term enterprise architecture. |
| Local/offline variant | Limited local processing for constrained connectivity environments. | Future phase, not MVP. |

### 7.2 Recommended MVP Deployment

For enterprise insurer pilots, the recommended deployment is:

> **Private Azure deployment / Azure Managed Application-ready architecture.**

For fast validation or internal demos, an Otic-hosted SaaS-like environment may be used, but it must still enforce tenant boundaries, real provider configuration, and production-safe controls.

### 7.3 Environment Strategy

| Environment | Purpose | Data Rules |
|---|---|---|
| Local | Developer execution with mocks where explicitly allowed. | Synthetic data only. |
| Dev | Shared development integration environment. | Synthetic or test data only. |
| Staging | Production-like validation environment. | No fake endpoints; controlled test data. |
| Pilot Production | First customer pilot environment. | Real customer data only under approved legal/security controls. |
| Production | Commercial customer deployments. | Full security, audit, monitoring, backup, and incident response. |

### 7.4 Local Developer Mode

Local developer mode may use mocked providers only when clearly configured as local development. It must not leak into staging or production.

Local mode may include:

- Mock AI provider.
- Mock document extraction provider.
- Local database / SQLite or local SQL Server.
- Local file storage.
- Seeded synthetic claims and documents.
- Provider health panel showing `local_mock` status.

Staging and production must never silently fall back to mocks.

---

## 8. Frontend Architecture

### 8.1 Recommended Stack

| Layer | Technology |
|---|---|
| Framework | Next.js App Router |
| Language | TypeScript |
| UI | React |
| Styling | Tailwind CSS |
| Data fetching | TanStack Query or equivalent query abstraction |
| Auth integration | MSAL / Microsoft Entra ID integration |
| Realtime updates | SignalR or WebSocket where needed |
| Forms | React Hook Form or similar |
| Validation | Zod or shared schema validation where practical |

### 8.2 UI Architecture Pattern

The target UI uses a three-panel structure:

```text
Persistent Top Bar
        |
Left Navigation | Main Work/Chat Workspace | Right Contextual Copilot Panel
        |
Status / Provider Health / Sync Indicators
```

### 8.3 Primary Routes

| Route | Screen |
|---|---|
| `/login` | Login and tenant selection. |
| `/command-center` | Operational command center. |
| `/channels` | Unified channel inbox. |
| `/customers` | Customer 360 list. |
| `/customers/[id]` | Customer 360 profile. |
| `/sales-leads` | Sales and lead inbox. |
| `/sales-leads/[id]` | Lead detail workspace. |
| `/claims` or `/work-queue` | Claims work queue. |
| `/claims/new` | FNOL / new claim intake. |
| `/claims/[id]` | Claim case review. |
| `/documents` | Document workspace. |
| `/documents/[id]/review` | Extraction review. |
| `/approvals` | Human approval queue. |
| `/knowledge` | Knowledge base. |
| `/audit` | Audit and compliance. |
| `/reports` | Reports and analytics. |
| `/integrations` | Integration configuration and health. |
| `/admin` | Admin settings. |
| `/support-health` | Provider and deployment health. |

### 8.4 Frontend State Requirements

Every major page must support:

- Loading state.
- Empty state.
- Error state.
- Unauthorized state.
- Not configured state.
- Partial provider failure state.
- Refresh/retry action.
- Tenant/workspace context display.
- Correlation ID propagation for support.

### 8.5 API Client Requirements

The frontend API client must:

- Use configured API base URL.
- Send credentials/tokens securely.
- Include tenant/workspace context where required.
- Handle 401, 403, 404, 409, 422, 429, and 500-class responses.
- Render useful errors rather than blank screens.
- Avoid client-side authorization as the only protection.

### 8.6 Current V1 Endpoint Expectations

The codebase-aware findings identify the following required endpoints:

```text
GET /api/v1/channels/overview
GET /api/v1/customers/overview
GET /api/v1/sales-leads/overview
```

These endpoints should be backed by real backend data in staging/production. Empty results should render empty states. Failed API calls should render error states.

---

## 9. Backend Architecture

### 9.1 Recommended Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 LTS |
| API framework | ASP.NET Core Web API |
| Persistence | EF Core with Azure SQL |
| Auth | Microsoft.Identity.Web / JWT Bearer validation |
| Background jobs | Azure Functions / Durable Functions, or hosted workers where appropriate |
| Validation | FluentValidation or equivalent |
| API documentation | OpenAPI / Swagger |
| Logging | Structured logging with Application Insights |
| Messaging | Azure Service Bus SDK |
| Storage | Azure Blob Storage SDK |
| Search | Azure AI Search SDK |
| AI | Azure OpenAI SDK / provider abstraction |

### 9.2 Backend Logical Services

| Service | Responsibility |
|---|---|
| IdentityContextService | Resolve authenticated user, tenant, workspace, roles, permissions. |
| TenantService | Tenant profile, country, currency, products, workflows, configuration. |
| ChannelService | Conversations, inbound messages, routing, channel status. |
| CustomerService | Customer 360, identity, contacts, consent, interactions. |
| SalesLeadService | Lead capture, qualification, routing, product interest, quote readiness. |
| ClaimService | Claims, statuses, assignment, SLA, lifecycle transitions. |
| DocumentService | Upload metadata, storage references, classification, extraction status. |
| DocumentProcessingService | OCR/extraction orchestration and validation. |
| ChecklistService | Required documents by claim type/product/tenant. |
| AiOrchestrationService | AI summaries, drafting, Q&A, tool routing, guardrails. |
| KnowledgeService | Policy/SOP/checklist indexing and retrieval. |
| FraudSignalService | Advisory risk/inconsistency flags. |
| ApprovalService | Human review tasks and decisions. |
| AuditService | Immutable-style audit events and traceability. |
| IntegrationService | External connector configuration, health, sync events. |
| ExportService | PDF/CSV/JSON/API claim pack export. |
| NotificationService | Customer-safe messaging through approved channels. |
| ProviderHealthService | AI, search, document intelligence, storage, messaging, graph, WhatsApp health. |

### 9.3 API Design Principles

- RESTful endpoints should be grouped by domain.
- Use `/api/v1` version prefix.
- Return consistent problem details for errors.
- Use pagination for list endpoints.
- Use correlation IDs for tracing.
- Validate tenant/workspace access server-side.
- All sensitive mutations must create audit events.
- Long-running work should return job/workflow IDs.
- AI outputs should store model/provider metadata and evidence IDs.

### 9.4 Example API Surface

| Domain | Endpoint | Method | Purpose |
|---|---|---|---|
| Health | `/health` | GET | Basic health. |
| Health | `/health/live` | GET | Liveness. |
| Health | `/health/ready` | GET | Readiness. |
| User | `/api/v1/me` | GET | Current user, tenant, roles, permissions. |
| Channels | `/api/v1/channels/overview` | GET | Channel metrics and inbox. |
| Customers | `/api/v1/customers/overview` | GET | Customer metrics and records. |
| Customers | `/api/v1/customers/{id}` | GET | Customer profile. |
| Sales Leads | `/api/v1/sales-leads/overview` | GET | Lead metrics and pipeline. |
| Claims | `/api/v1/claims` | GET | Claims work queue. |
| Claims | `/api/v1/claims` | POST | Create claim / FNOL. |
| Claims | `/api/v1/claims/{id}` | GET | Claim case details. |
| Claims | `/api/v1/claims/{id}/assign` | POST | Assign claim. |
| Claims | `/api/v1/claims/{id}/escalate` | POST | Escalate claim. |
| Documents | `/api/v1/claims/{id}/documents` | POST | Upload claim document metadata/upload request. |
| Documents | `/api/v1/documents/{id}/process` | POST | Start document processing. |
| Documents | `/api/v1/documents/{id}/extraction` | GET | Get extraction results. |
| AI | `/api/v1/claims/{id}/summary` | POST | Generate or refresh AI claim summary. |
| Approvals | `/api/v1/approvals` | GET | Human approval queue. |
| Approvals | `/api/v1/approvals/{id}/decision` | POST | Approve/reject/escalate. |
| Audit | `/api/v1/audit/events` | GET | Search audit events. |
| Integrations | `/api/v1/integrations/health` | GET | Connector health. |
| Admin | `/api/v1/admin/provider-health` | GET | Provider configuration/health. |

---

## 10. Data Architecture

### 10.1 Data Store Responsibilities

| Store | Data |
|---|---|
| Azure SQL | Tenants, users, roles, customers, policies, claims, leads, conversations, tasks, approvals, audit metadata, integration events. |
| Azure Blob Storage | Original documents, normalized documents, evidence files, exports, generated packs. |
| Azure AI Search | Indexed policy documents, SOPs, claim evidence, extracted text, knowledge base chunks. |
| Azure Service Bus | Workflow messages, document processing jobs, notification events, integration retry events. |
| Application Insights / Log Analytics | Logs, traces, metrics, exceptions, dependency telemetry. |
| Key Vault | Secrets, API keys, certificates, connection strings. |

### 10.2 Canonical Entities

| Entity | Purpose |
|---|---|
| Tenant | Insurer/customer deployment or logical customer. |
| Workspace | Branch, region, product line, business unit, or operational workspace. |
| User | Internal insurer or platform user. |
| Role | Permission grouping. |
| Permission | Fine-grained action control. |
| Customer | Policyholder/prospect/customer profile. |
| Policy | Policy reference, schedule, coverage, terms, external reference. |
| Claim | Insurance claim case. |
| ClaimEvent | Timeline event in claim lifecycle. |
| Document | Uploaded or imported file metadata. |
| DocumentExtraction | OCR and field extraction outputs. |
| DocumentChecklistItem | Required/optional document status by claim type. |
| ChannelConversation | External channel conversation thread. |
| ChannelMessage | Individual inbound/outbound message. |
| SalesLead | Acquisition lead created from channel/broker/staff interaction. |
| Task | Work item assigned to staff. |
| Approval | Human review and decision task. |
| AiRun | AI model/tool invocation record. |
| EvidenceReference | Link from AI output to source record/document/chunk/field. |
| FraudSignal | Advisory risk/inconsistency flag. |
| AuditEvent | Immutable-style event for user, system, AI, and integration actions. |
| IntegrationEvent | External sync/import/export event. |
| ExportPackage | Generated PDF/CSV/JSON/API export package. |
| ConsentRecord | Customer consent and purpose record. |
| RetentionPolicy | Tenant/workspace data retention configuration. |

### 10.3 Required Data Attributes

Every major business entity should include:

- `id`
- `tenantId`
- `workspaceId`
- `createdAt`
- `createdBy`
- `updatedAt`
- `updatedBy`
- `status`
- `sourceSystem`
- `externalReference`
- `correlationId` where applicable

Every sensitive workflow action should include:

- actor user ID
- actor role
- tenant/workspace
- action type
- before/after value where safe
- evidence reference
- approval reference where applicable
- model/provider metadata for AI actions
- timestamp
- correlation ID

### 10.4 Data Classification

| Classification | Examples | Controls |
|---|---|---|
| Public | Marketing copy, generic product descriptions | Normal controls. |
| Internal | System configuration, non-sensitive logs | Staff-only access. |
| Confidential | Claims, policies, customer records, internal notes | RBAC, tenant isolation, encryption, audit. |
| Restricted | National ID, medical information, payment details, fraud signals | Strong access control, masking, approval, audit, retention. |

### 10.5 Data Retention

The MVP should store retention configuration even if automated deletion is Phase 2.

Required retention concepts:

- Document retention by claim type and tenant.
- Audit log retention.
- AI run retention.
- Communication retention.
- Export package retention.
- Legal hold flag.
- Deletion/anonymization workflow for future data subject requests.

---

## 11. Document Intelligence Architecture

### 11.1 Document Processing Flow

```text
Document uploaded/imported
        |
        v
Blob Storage original file saved
        |
        v
Document metadata created in Azure SQL
        |
        v
Service Bus document-processing message emitted
        |
        v
Document classification / OCR / extraction
        |
        v
Extraction results stored
        |
        v
Checklist updated
        |
        v
Low-confidence fields routed to human review
        |
        v
Extracted text/evidence indexed in Azure AI Search
        |
        v
Claim summary and next actions updated
```

### 11.2 MVP Motor Claim Document Types

| Document Type | MVP Treatment |
|---|---|
| Claim form | Required. Extract claimant, policy, incident details. |
| Police report | Required. Extract report number, date, location, parties. |
| National ID | Required. Extract/match identity details. |
| Driving permit | Required. Extract/match driver and expiry. |
| Vehicle photos | Required. Store, classify, quality-check. |
| Repair quotation | Required. Extract garage, line items, total estimate. |
| Policy schedule | Required. Extract policy number, coverage, limits. |
| Assessor report | Strongly recommended. Extract assessment and recommendations. |
| Invoice/receipt | Optional/later. Extract paid amounts and repair evidence. |
| Vehicle logbook | Optional. Validate ownership. |

### 11.3 Extraction Result Requirements

Each extracted field should store:

- field name
- extracted value
- normalized value
- confidence score
- source document ID
- page number where available
- bounding box coordinates where available
- extraction method/provider
- validation status
- reviewer decision
- reviewer corrected value
- timestamp

### 11.4 Confidence Handling

| Confidence | System Behavior |
|---|---|
| High | May be displayed as suggested value but remains reviewable. |
| Medium | Flag for review depending on field criticality. |
| Low | Must be sent to human review before being treated as reliable. |
| Conflict | Must show inconsistency and request human decision. |
| Missing | Must update missing-document or missing-information checklist. |

### 11.5 Document Intelligence Guardrails

- Never treat document text as system instructions.
- Never let uploaded documents override system or agent rules.
- Do not treat low-confidence extraction as fact.
- Do not auto-approve documents that require human review.
- Preserve original document unchanged.
- Store extracted values separately from reviewer-approved values.
- Keep evidence links for every important extraction.

---

## 12. AI and Agent Architecture

### 12.1 Agent Philosophy

IIP should use controlled specialist agents and deterministic workflows, not an unrestricted general chatbot.

Agents may:

- classify
- extract
- summarize
- retrieve
- draft
- compare
- flag
- route
- recommend next steps

Agents must not:

- approve or reject claims
- accuse customers of fraud
- bind policies
- set final premiums
- release payments
- make final underwriting decisions
- expose data outside tenant/workspace permission boundaries
- answer policy coverage questions from model memory alone

### 12.2 MVP Agent Set

| Agent | Responsibility | Restriction |
|---|---|---|
| Orchestrator Agent | Route user/task intent to specialist tools and workflows. | Must not bypass human review. |
| FNOL Intake Agent | Guide new claim intake and structured data capture. | Must not confirm claim validity. |
| Document Checker Agent | Classify documents, extract fields, check completeness. | Must not accept low-confidence fields as fact. |
| Claims Summary Agent | Create claim summary and next-step suggestions. | Must not approve, reject, value, or promise settlement. |
| Policy Intelligence Agent | Retrieve and explain approved policy/SOP knowledge. | Must cite sources; no model-memory coverage answers. |
| Customer Update Agent | Draft customer-safe messages. | Human approval required for sensitive/adverse messages. |
| Fraud Signal Agent | Flag inconsistencies and suspicious patterns. | Must not accuse; outputs are advisory signals. |
| Audit Agent / Audit Service | Record AI outputs, tools, evidence, and human actions. | Must not hide or alter logs. |

### 12.3 Agent Tool Registry

Agents must only call approved tools from a registry. Generic database tools are prohibited.

Each tool definition must include:

- tool name
- purpose
- input schema
- output schema
- authorized agents
- authorized roles
- tenant/workspace scope
- data classification
- timeout policy
- retry policy
- failure behavior
- audit logging rules
- human review requirement

Example tools:

| Tool | Purpose |
|---|---|
| `get_claim(claimId)` | Retrieve claim metadata within tenant scope. |
| `get_claim_documents(claimId)` | Retrieve document metadata for a claim. |
| `get_required_documents(claimType, productType)` | Retrieve checklist for claim type. |
| `get_policy_context(policyId)` | Retrieve policy context and indexed knowledge. |
| `extract_document_fields(documentId)` | Start or retrieve document extraction. |
| `generate_claim_summary(claimId)` | Generate structured claim summary. |
| `detect_fraud_signals(claimId)` | Generate advisory risk flags. |
| `draft_customer_update(claimId, purpose)` | Draft customer-safe update for review. |
| `create_approval_task(actionType, subjectId)` | Create human review task. |
| `log_ai_run(metadata)` | Store AI run and evidence trace. |

### 12.4 RAG and Grounding Architecture

```text
User asks question / workflow requests AI output
        |
        v
Authorize user + tenant + workspace + object scope
        |
        v
Retrieve approved evidence from Azure AI Search / SQL / Blob metadata
        |
        v
Construct prompt with system policy, role, task, evidence, output schema
        |
        v
Azure OpenAI generates structured answer
        |
        v
Validate output schema and policy guardrails
        |
        v
Store AI run, evidence IDs, model metadata, confidence, and output
        |
        v
Show output as AI suggestion, not final decision
```

### 12.5 AI Run Metadata

Every AI run should log:

- AI run ID
- tenant ID
- workspace ID
- user ID or system actor
- agent name
- prompt version
- tool calls
- evidence IDs
- model deployment
- provider
- input hash / redacted input reference
- output
- confidence or uncertainty
- safety/guardrail flags
- latency
- token usage / estimated cost
- final user action

---

## 13. Workflow and Event Architecture

### 13.1 Workflow Principles

- Long-running workflows should be asynchronous.
- Human review should pause workflow until action is taken.
- Timeouts and SLA thresholds should create tasks/escalations.
- External provider failures should be retryable where safe.
- Workflow state should be visible to users.
- Every workflow transition should be auditable.

### 13.2 Core MVP Workflows

#### Workflow A: WhatsApp / Channel FNOL Intake

```text
Inbound customer message
        |
        v
Channel webhook receives message
        |
        v
Message stored + audit event created
        |
        v
Intent classified as claim / quote / service / complaint
        |
        v
If claim: create or update FNOL work item
        |
        v
Request required documents
        |
        v
Route to claims work queue
```

#### Workflow B: Document Processing

```text
Document uploaded
        |
        v
Blob saved + Document record created
        |
        v
Processing job queued
        |
        v
Document Intelligence extraction
        |
        v
Validation and confidence scoring
        |
        v
Checklist update
        |
        v
Review task created for low-confidence/conflicting fields
```

#### Workflow C: Claim Review

```text
Claim enters work queue
        |
        v
Officer opens case review
        |
        v
AI summary and evidence displayed
        |
        v
Officer reviews documents and extracted fields
        |
        v
Officer requests missing info, escalates, or prepares next step
        |
        v
Sensitive action creates approval task
        |
        v
Supervisor approves/rejects/returns/escalates
        |
        v
Audit trail updated
```

#### Workflow D: Customer Update Drafting

```text
System identifies update need
        |
        v
AI drafts customer-safe message
        |
        v
Message displayed to authorized officer
        |
        v
Officer edits/approves
        |
        v
Message sent via approved channel
        |
        v
Communication stored and audited
```

### 13.3 Service Bus Queues / Topics

Recommended queue/topic structure:

| Queue/Topic | Purpose |
|---|---|
| `channel-inbound` | Incoming messages from WhatsApp/email/web. |
| `document-processing` | Document OCR/classification/extraction jobs. |
| `ai-jobs` | Async AI summarization/drafting jobs. |
| `notifications-outbound` | Customer/staff notifications. |
| `integration-events` | External system imports/exports/syncs. |
| `audit-events` | Optional buffered audit event ingestion. |
| `dead-letter-review` | Failed events requiring support review. |

---

## 14. Integration Architecture

### 14.1 Integration Strategy

IIP should avoid requiring deep custom integration into every insurer core system for the MVP. The recommended strategy is a flexible integration fabric:

- WhatsApp Business API for customer conversations.
- Microsoft Graph for Outlook/shared mailbox ingestion.
- Secure file upload for documents.
- CSV/Excel import for controlled batch data.
- API integration where customer systems expose safe endpoints.
- Read-only policy/claims lookup where possible.
- Controlled export/write-back only after human approval.

### 14.2 MVP Integrations

| Integration | Purpose | Direction | Priority |
|---|---|---|---|
| WhatsApp Business API | Customer claim initiation, document requests, status messages. | Two-way | Critical |
| Microsoft Graph / Outlook | Shared mailbox ingestion and approved outbound email. | Read/write with controls | High |
| Azure Blob Storage | Document and evidence storage. | Read/write | Critical |
| Azure AI Document Intelligence | OCR/extraction. | Processing | Critical |
| Azure AI Search | Knowledge/evidence retrieval. | Query/index | Critical |
| Core policy system | Policy validation. | Prefer read-only | Phase 1/2 depending on pilot |
| Claims system | Export or sync reviewed claim pack. | Controlled export/write-back | Phase 2 |

### 14.3 Future Integrations

| Integration | Use |
|---|---|
| SMS gateway | Fallback customer notifications. |
| Mobile money | Premium payments or claim payout workflows, only with strict approvals. |
| Power BI / Fabric | Advanced analytics and executive reporting. |
| SharePoint / OneDrive | Policy document and evidence import/export. |
| Teams | Internal alerts and workflow notifications. |
| CRM | Customer/prospect/lead synchronization. |
| Regulatory systems | Claims/complaints reporting exports. |
| Policy admin vendors | Read-only lookup and controlled updates. |

### 14.4 Integration Event Requirements

Every integration event should capture:

- integration event ID
- tenant ID
- workspace ID
- connector name
- external system
- direction
- payload type
- source record ID
- target record ID
- status
- error code/message
- retry count
- created timestamp
- completed timestamp
- correlation ID

---

## 15. Security Architecture

### 15.1 Identity and Authentication

IIP should use Microsoft Entra ID for staff authentication. Customer-facing WhatsApp flows should use channel-specific verification and should not imply access to the internal app.

Requirements:

- Staff SSO with Microsoft Entra ID.
- MFA-ready configuration.
- Token validation in backend.
- Correct issuer/audience validation.
- Role and tenant claims resolved server-side.
- Session timeout and secure cookies/tokens.
- Support for customer-specific Entra tenant in enterprise deployments where required.

### 15.2 Authorization Model

Use RBAC plus selected ABAC attributes.

RBAC roles may include:

- Platform Admin
- Insurer Admin
- Claims Officer
- Claims Supervisor
- Customer Service Agent
- Underwriter
- Compliance Officer
- Executive Viewer
- Support Engineer
- Integration Admin
- Knowledge Manager

ABAC attributes may include:

- tenant
- workspace
- branch
- product line
- claim type
- claim value
- document sensitivity
- assignment
- approval limit
- workflow stage

### 15.3 Sensitive Action Controls

Sensitive actions must require explicit permission and audit logging.

| Action | Control |
|---|---|
| View restricted document | Permission + audit. |
| Edit extracted field | Permission + old/new value audit. |
| Approve field extraction | Permission + evidence reference. |
| Escalate claim | Permission + reason. |
| Prepare adverse customer message | Human approval. |
| Approve settlement recommendation | Human approval; AI cannot finalize. |
| Export claim pack | Permission + export audit. |
| Configure AI provider | Admin permission + audit. |
| Configure integration | Integration admin permission + audit. |

### 15.4 Secrets Management

All secrets must be stored in Key Vault, including:

- database connection strings
- API keys
- WhatsApp secrets
- Graph app secrets/certificates
- Azure OpenAI keys if managed identity is unavailable
- Document Intelligence keys if managed identity is unavailable
- signing keys/certificates

Managed Identity should be used wherever possible.

### 15.5 Encryption

| Data State | Requirement |
|---|---|
| In transit | TLS 1.2+ minimum. |
| At rest | Azure-managed encryption by default; customer-managed keys for enterprise option. |
| Blob documents | Encryption at rest, private access, SAS minimized. |
| Database | Transparent Data Encryption; consider Always Encrypted for restricted fields. |
| Backups | Encrypted and access-controlled. |

### 15.6 Data Masking

PII and restricted fields should be masked in:

- list views
- logs
- telemetry
- support exports
- AI prompts where not required
- non-privileged user views

Examples:

- National ID masked except last 4 characters.
- Phone/email masked in audit views where not needed.
- Medical details restricted to authorized roles.

### 15.7 Prompt Injection and AI Security

Controls required:

- Treat uploaded documents as untrusted content.
- Never execute instructions from documents/messages as system instructions.
- Use strict system prompts and tool schemas.
- Limit agent tools by role, tenant, and workflow state.
- Validate AI output with schemas.
- Log tool calls and evidence references.
- Prevent cross-tenant retrieval.
- Exclude secrets and hidden system instructions from AI context.

---

## 16. Compliance and Audit Architecture

### 16.1 Audit Scope

Audit events must cover:

- user login/logout
- tenant/workspace changes
- claim creation/update/status change
- document upload/process/review
- field extraction approval/edit/rejection
- AI summary generation
- AI draft generation
- fraud signal creation/review
- approval decisions
- customer communication sent
- export package generation
- integration import/export
- admin setting changes
- provider configuration changes
- role/permission changes

### 16.2 Audit Event Schema

Each audit event should include:

- audit event ID
- tenant ID
- workspace ID
- actor type: user/system/agent/integration
- actor ID
- actor role
- action
- subject type
- subject ID
- before value reference where safe
- after value reference where safe
- reason/comment
- evidence IDs
- AI run ID where applicable
- IP/device metadata where applicable
- timestamp
- correlation ID

### 16.3 Claims Register Support

The system should be able to produce a claims register export containing at least:

- claim number
- insured/customer name
- policy number
- class/product line
- date of loss
- date reported
- location/place of loss
- claim status
- claim handler
- assessor/surveyor where applicable
- required documents status
- settlement/repudiation dates where applicable
- amounts where available
- audit references

Final required format must be confirmed with legal/regulatory review and pilot customer requirements.

---

## 17. Tenancy and Workspace Architecture

### 17.1 Tenant Model

A tenant represents an insurer/customer deployment or logical insurer customer.

Every record must be tenant-scoped:

- customers
- policies
- claims
- documents
- leads
- conversations
- tasks
- approvals
- audit events
- AI runs
- integration events
- knowledge base content
- configuration

### 17.2 Workspace Model

A workspace represents a business segmentation under a tenant, such as:

- branch
- region
- product line
- business unit
- broker network
- country
- department

Workspace support allows one insurer to operate multiple controlled operational contexts.

### 17.3 Isolation Controls

| Control | Requirement |
|---|---|
| Backend filters | Tenant/workspace filters enforced server-side. |
| Database constraints | Tenant ID required on major records. |
| Search index filters | Tenant/workspace filters required for all search queries. |
| Blob paths | Tenant/workspace-scoped containers or prefixes. |
| Audit events | Tenant/workspace stored for all actions. |
| AI retrieval | No cross-tenant evidence retrieval. |
| Support access | Just-in-time, audited, least privilege. |

---

## 18. Observability and Operations

### 18.1 Health Endpoints

Required endpoints:

```text
GET /health
GET /health/live
GET /health/ready
GET /api/v1/admin/provider-health
GET /api/v1/integrations/health
```

### 18.2 Provider Health Checks

Provider health should include:

- Azure SQL
- Blob Storage
- Service Bus
- Azure AI Search
- Azure OpenAI
- Document Intelligence
- Microsoft Graph
- WhatsApp provider
- Key Vault
- background worker status
- queue backlog

Statuses:

- connected
- degraded
- disconnected
- not configured
- unauthorized
- quota exceeded

### 18.3 Telemetry Requirements

Track:

- request latency
- API error rates
- frontend route failures
- document processing time
- AI latency and cost
- token usage
- queue depth
- failed jobs
- provider errors
- claim workflow cycle time
- SLA breach risk
- user actions
- export activity

### 18.4 Alerts

Recommended alerts:

| Alert | Trigger |
|---|---|
| API error spike | 5xx rate above threshold. |
| Auth failure spike | Unusual 401/403 increase. |
| Document processing backlog | Queue length above threshold. |
| AI provider failure | Provider degraded/disconnected. |
| Search indexing failure | Failed indexer/job count above threshold. |
| Blob upload failure | Upload failures above threshold. |
| Integration failure | Graph/WhatsApp/core connector repeated failures. |
| SLA risk | Claims nearing SLA breach. |
| Audit write failure | Any audit event persistence failure. |

### 18.5 Support and Incident Response

Every error path should include correlation ID support.

Support engineers should be able to see:

- deployment health
- provider health
- queue status
- failed jobs
- recent errors
- integration failures
- customer/tenant impact
- audit trail for sensitive support actions

---

## 19. Resilience and Hybrid Cloud Mirror Concept

### 19.1 Problem

Insurer offices and customer service channels may experience unreliable connectivity. Client feedback has raised concern that WhatsApp bots and channel workflows should not lose messages during outages.

### 19.2 MVP-Safe Framing

The MVP should not claim full offline platform operation. Instead, it should support or design toward:

> **Resilient Channel Buffering / Hybrid Cloud Mirror**

This means:

- inbound webhook events are acknowledged and stored reliably where possible
- messages are queued
- failed processing is retried
- dead-letter queues preserve failures for support review
- local or edge buffering can be added for specific deployments
- users can see channel health and degraded states

### 19.3 Cloud Queue Architecture

```text
WhatsApp / Channel Provider
        |
        v
Webhook Receiver
        |
        v
Persist raw inbound event
        |
        v
Service Bus queue
        |
        v
Processing worker
        |
        v
Conversation / Lead / Claim / Task update
```

### 19.4 Future Local Buffer Architecture

```text
Local office / edge service
        |
        v
Local queue or durable store
        |
        v
Sync agent with retry/backoff
        |
        v
Cloud ingestion endpoint
        |
        v
IIP channel processing pipeline
```

This should be treated as an enterprise add-on or Phase 2/3 resilience feature unless a pilot customer explicitly requires it.

---

## 20. DevOps and CI/CD

### 20.1 Repository Structure Recommendation

```text
/iip
  /apps
    /web                 # Next.js frontend
    /api                 # .NET 8 backend API
  /workers
    /document-worker     # Functions / background processing
    /integration-worker  # Integration processing
  /infra
    /bicep or /terraform # Azure IaC
  /docs
    /architecture
    /product
    /security
    /api
  /tests
    /unit
    /integration
    /e2e
```

### 20.2 Pipeline Stages

| Stage | Required Checks |
|---|---|
| Build | Compile frontend/backend/workers. |
| Lint | TypeScript, C#, formatting. |
| Unit tests | Domain and service tests. |
| Integration tests | API/database/provider abstractions. |
| Security scan | Dependencies, secrets, static analysis. |
| IaC validation | Bicep/Terraform validation. |
| Deploy dev | Automatic for main/dev branch. |
| Deploy staging | Controlled approval. |
| Smoke tests | Health, login, key APIs, provider health. |
| Deploy production | Manual approval and rollback plan. |

### 20.3 Release Controls

- Use branch protection.
- Require code review.
- Require passing tests.
- Require environment approvals for staging/production.
- Track release version.
- Generate release notes.
- Maintain rollback plan.
- Store deployment metadata.

### 20.4 Infrastructure as Code

Use Bicep or Terraform to provision:

- resource groups
- app hosting
- Azure SQL
- storage
- service bus
- key vault
- application insights
- log analytics
- AI Search
- Document Intelligence
- API Management where required
- managed identities
- RBAC assignments
- private endpoints where required

---

## 21. Performance, Scalability, and Reliability

### 21.1 Performance Targets

Initial MVP targets should be validated during testing.

| Area | Target Direction |
|---|---|
| Dashboard load | Under 3 seconds for normal tenant data. |
| Claims list load | Under 3 seconds with pagination. |
| Claim detail load | Under 4 seconds excluding heavy document previews. |
| Document upload acknowledgement | Under 5 seconds after upload completion. |
| Document processing | Async; progress visible. |
| AI summary generation | Async or under defined timeout with retry. |
| Search response | Under 2 seconds for typical knowledge query. |

### 21.2 Scalability Strategy

- Paginate all list views.
- Use async processing for documents and AI jobs.
- Use queues for bursty ingestion.
- Scale API and workers independently.
- Partition search indexes by tenant or use strict filters depending on deployment model.
- Store large files in Blob Storage, not database.
- Cache low-risk reference data where safe.

### 21.3 Reliability Requirements

- Health checks for every critical provider.
- Dead-letter queues for failed async work.
- Retry with exponential backoff for transient failures.
- Idempotency keys for inbound events and external callbacks.
- Backup and restore tested for Azure SQL and Blob Storage.
- Audit event writes should be highly reliable and monitored.

---

## 22. Non-Functional Requirements

| Category | Requirement |
|---|---|
| Availability | MVP target to be agreed; production should design toward enterprise availability. |
| Security | Entra ID, RBAC/ABAC, Key Vault, encryption, audit logs, least privilege. |
| Privacy | Data minimization, masking, consent records, retention planning. |
| Compliance | Claims audit trail, human approvals, source-grounded AI, regulator-ready exports. |
| Accessibility | UI should target WCAG 2.1 AA where practical. |
| Observability | Logs, metrics, traces, alerts, provider health, correlation IDs. |
| Maintainability | Modular services, clear interfaces, documented APIs, IaC. |
| Extensibility | Modules and agents should be feature-flagged and configurable. |
| Portability | Architecture should support Otic-hosted SaaS and customer-private Azure deployment. |
| Resilience | Queue-based async processing, retries, dead-letter review, backup/restore. |
| Cost control | Track AI token usage, document processing volume, storage, search, and compute costs. |

---

## 23. Configuration and Feature Flags

### 23.1 Required Configuration Areas

- tenant profile
- country/currency
- workspace structure
- roles and permissions
- claim types
- product lines
- document checklists
- SLA rules
- approval thresholds
- AI provider settings
- model deployments
- search indexes
- WhatsApp provider
- Graph/mailbox settings
- storage containers
- retention rules
- support access controls

### 23.2 Feature Flags

Recommended flags:

| Flag | Purpose |
|---|---|
| `claims.motor.enabled` | Enable motor claims workflow. |
| `channels.whatsapp.enabled` | Enable WhatsApp intake. |
| `channels.email.enabled` | Enable email ingestion. |
| `ai.claimSummary.enabled` | Enable AI claim summaries. |
| `ai.policyQa.enabled` | Enable policy Q&A. |
| `fraudSignals.enabled` | Enable advisory fraud signals. |
| `salesLeads.enabled` | Enable Sales and Leads. |
| `customers360.enabled` | Enable Customer 360. |
| `earlyWarning.enabled` | Future retention module. |
| `underwritingCopilot.enabled` | Future underwriting module. |
| `mockProviders.localOnly` | Only valid in local development. |

---

## 24. Testing Architecture

### 24.1 Test Types

| Test Type | Coverage |
|---|---|
| Unit tests | Domain rules, validators, services, utilities. |
| Integration tests | API + database + storage abstractions. |
| Contract tests | API request/response schemas. |
| E2E tests | Login, claim creation, document upload, review, audit. |
| Security tests | Auth, authorization, tenant isolation, injection attempts. |
| AI tests | Prompt output schema, refusal boundaries, source citation behavior. |
| Document tests | OCR/extraction on synthetic motor claim documents. |
| Load tests | Channels, claims list, document processing queues. |
| Resilience tests | Provider outage, queue retry, dead-letter handling. |
| Accessibility tests | Keyboard navigation, contrast, focus states. |

### 24.2 MVP Test Scenarios

- Create motor claim from internal UI.
- Create claim from channel conversation.
- Upload required documents.
- Process document extraction.
- Show low-confidence fields for review.
- Generate claim summary with evidence links.
- Flag missing police report.
- Flag mismatched date between claim form and police report.
- Draft customer missing-document message.
- Human approves message.
- Audit trail shows all actions.
- Export claim pack.
- Unauthorized user cannot view restricted claim.
- Cross-tenant access attempt fails.
- AI refuses to approve/reject claim.

---

## 25. Cost Management Architecture

### 25.1 Cost Drivers

| Cost Driver | Control |
|---|---|
| Azure OpenAI tokens | Track per tenant, agent, workflow, user. |
| Document Intelligence pages | Track per document and tenant. |
| AI Search indexes | Manage index size and retention. |
| Blob Storage | Retention policies and lifecycle management. |
| SQL Database | Right-size tier and monitor DTU/vCore usage. |
| Service Bus | Monitor queue operations and backlog. |
| App hosting | Autoscale conservatively. |
| Monitoring/logs | Retention and sampling policies. |
| WhatsApp provider | Track conversations and message templates. |

### 25.2 Cost Telemetry

Store usage records for:

- AI runs
- token counts
- document pages processed
- documents stored
- search queries
- channel messages
- WhatsApp conversations
- export packages
- active users
- claims processed

This enables future billing and ROI reporting.

---

## 26. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| AI gives unsupported insurance advice | Regulatory/customer harm | RAG-only policy answers, source citations, human escalation. |
| AI appears to make final decision | Governance risk | UI labels, approval gates, audit logs, strict prompts. |
| Cross-tenant data leakage | Severe security breach | Tenant filters, tests, search filters, separate storage/index strategy. |
| Azure OpenAI region/quotas unavailable | Deployment delay | Confirm quotas early; define fallback region. |
| Insurer core system lacks API | Integration delay | Use CSV/Excel/SFTP/manual export fallback. |
| Document extraction accuracy too low | Poor user trust | Human review, confidence thresholds, sample packs, iterative training. |
| WhatsApp provider setup delayed | Channel delay | Support email/web/manual intake fallback. |
| Legal/data residency uncertainty | Sales/deployment blocker | Legal review before pilot. |
| Scope creep into underwriting/fraud/early warning | MVP delay | Feature flags and roadmap separation. |
| Production accidentally uses mock providers | Trust/security issue | Environment validation and startup checks. |

---

## 27. Open Technical Decisions

| ID | Decision | Owner | Priority |
|---|---|---|---|
| TD-001 | Confirm final Azure region and model quotas. | Cloud Architect | High |
| TD-002 | Confirm first pilot deployment model: private Azure, Managed App, or Otic-hosted pilot. | Leadership / Cloud | High |
| TD-003 | Confirm WhatsApp provider: Meta direct, Twilio, Infobip, or other. | Engineering | High |
| TD-004 | Confirm policy admin/claims system integration method for pilot customer. | Solutions / Customer IT | High |
| TD-005 | Choose Bicep vs Terraform for IaC. | Engineering | Medium |
| TD-006 | Choose SignalR vs polling for realtime work queue updates. | Engineering | Medium |
| TD-007 | Decide search index isolation strategy: per tenant vs filtered shared index. | Architecture / Security | High |
| TD-008 | Confirm audit storage strategy: SQL ledger, append-only tables, or event store. | Architecture / Security | High |
| TD-009 | Confirm document extraction custom model training approach. | AI / Engineering | Medium |
| TD-010 | Confirm managed application packaging timeline. | Leadership / Cloud | Medium |

---

## 28. Implementation Sequence

### Phase 0 - Foundation

- Confirm naming, architecture, MVP boundaries.
- Confirm Azure region and subscription.
- Set up repository structure.
- Establish CI/CD baseline.
- Create dev/staging environments.
- Implement auth and `/api/v1/me`.
- Implement tenant/workspace context.
- Implement audit logging foundation.

### Phase 1 - Core Workspaces

- Command Center.
- Channels overview endpoint and UI.
- Customers overview endpoint and UI.
- Sales Leads overview endpoint and UI.
- Claims Work Queue.
- Provider health UI.
- Loading/empty/error/unauthorized/not configured states.

### Phase 2 - Claims and Documents

- FNOL / New Claim Intake.
- Claim detail workspace.
- Document upload to Blob Storage.
- Document metadata in SQL.
- Document processing queue.
- Azure AI Document Intelligence integration.
- Extraction review UI.
- Missing-document checklist.

### Phase 3 - AI and Human Review

- AI claim summary.
- Policy/SOP retrieval through Azure AI Search.
- Customer update drafting.
- Fraud-signal advisory flags.
- Approval queue.
- Evidence-linked AI outputs.
- AI run logging.

### Phase 4 - Integrations and Exports

- WhatsApp provider integration.
- Microsoft Graph shared mailbox ingestion.
- Claim pack PDF/CSV/JSON export.
- Integration event logging.
- Dead-letter review.
- Support/health dashboards.

### Phase 5 - Pilot Hardening

- Security testing.
- Tenant isolation testing.
- Load and resilience testing.
- Legal/compliance review support.
- Pilot data setup.
- User training.
- Runbook creation.
- Production deployment checklist.

---

## 29. Technical Acceptance Criteria

The MVP architecture is acceptable when:

1. Staff can authenticate through Microsoft Entra ID.
2. Backend validates issuer, audience, tenant, and roles correctly.
3. Every sensitive endpoint enforces server-side authorization.
4. Tenant/workspace context is stored and enforced on major entities.
5. Channels, Customers, Sales Leads, and Claims use real API endpoints in staging/production.
6. Documents are stored in Blob Storage and tracked in SQL.
7. Document processing is asynchronous and visible to the user.
8. AI summaries are evidence-linked and stored with AI run metadata.
9. AI refuses or blocks final regulated decisions.
10. Human approval tasks are created for sensitive actions.
11. Audit events are written for user, AI, workflow, document, and integration actions.
12. Provider health shows configured, degraded, disconnected, and not-configured states.
13. Errors include correlation IDs.
14. The platform supports loading, empty, error, unauthorized, and not-configured states.
15. No production build silently uses mock providers.
16. Basic claim pack export works.
17. Admin/support users can inspect health and failed jobs.
18. Tenant isolation and cross-tenant access tests pass.

---

## 30. Appendices

### Appendix A - Required Follow-On Technical Files

| File | Purpose |
|---|---|
| `openapi.yaml` | Full API contract. |
| `database_model.md` | Entity/table definitions and relationships. |
| `integration_strategy.md` | Connector specifications and fallback modes. |
| `ai_agent_tool_registry.md` | Approved AI tools and schemas. |
| `security_privacy_compliance.md` | Detailed security and compliance requirements. |
| `deployment_runbook.md` | Deployment and operational procedures. |
| `environment_variables.md` | Required configuration and secret names. |
| `qa_test_strategy.md` | Testing plan and acceptance scenarios. |
| `bicep_or_terraform_spec.md` | Infrastructure module definitions. |
| `incident_response_runbook.md` | Incident handling and escalation procedures. |

### Appendix B - Environment Variables and Secrets Categories

Do not commit secret values. The implementation should document names and purpose only.

| Category | Examples |
|---|---|
| API | API base URL, CORS origins, environment name. |
| Auth | Entra tenant ID, client ID, authority, audience. |
| Database | SQL connection string / managed identity config. |
| Storage | Blob account/container names. |
| AI | Azure OpenAI endpoint, deployment names. |
| Search | AI Search endpoint, index names. |
| Document AI | Document Intelligence endpoint/model IDs. |
| Messaging | Service Bus namespace/queue names. |
| Graph | Mailbox IDs, app registration details. |
| WhatsApp | Provider endpoint, webhook secret, phone number ID. |
| Observability | App Insights connection string. |

### Appendix C - Technical Glossary

| Term | Meaning |
|---|---|
| FNOL | First Notice of Loss; the first report of an insurance claim. |
| RAG | Retrieval-Augmented Generation; AI answers grounded in retrieved approved sources. |
| HITL | Human-in-the-loop; human review/approval before consequential action. |
| Tenant | Insurer/customer boundary. |
| Workspace | Branch/region/product/department context within a tenant. |
| Claim Pack | Exportable set of claim details, documents, evidence, summaries, and audit references. |
| Provider Health | Status of external/internal services such as AI, search, storage, Graph, WhatsApp. |
| Dead Letter Queue | Queue for messages that failed processing after retries. |
| Managed Application | Azure deployment model where software is deployed into customer subscription with managed support access. |

---

## 31. Closing Architecture Position

IIP should be built as a controlled, evidence-linked, Azure-native insurance operations platform. The architecture must support practical MVP delivery while preserving enterprise-grade security, auditability, configurability, and future expansion.

The safest and most commercially credible technical posture is:

> **Use Azure-native services, keep insurer data tenant-scoped, ground AI in approved evidence, make every consequential decision human-reviewed, and make every important action auditable.**