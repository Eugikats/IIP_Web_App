# Insurance Intelligence Platform (IIP)
## Data Model and Canonical Entity Specification

**Document Type:** Data Model / Canonical Entity Specification  
**Product:** Insurance Intelligence Platform (IIP)  
**Version:** 1.0  
**Date:** 30 June 2026  
**Prepared For:** IIP Product, Engineering, Architecture, AI, Security, Integration, and Delivery Teams  
**Classification:** Internal - Engineering and Product Reference  

---

## 0. Document Control

| Field | Value |
|---|---|
| Official product name | Insurance Intelligence Platform |
| Acronym | IIP |
| Superseded naming | ACIP / Agentic Insurance Intelligence Platform should be treated as an earlier naming direction |
| Primary deployment target | Azure-first insurance operations platform |
| Primary MVP wedge | Motor Claims and Document Intelligence |
| Primary users | Insurer staff, claims officers, supervisors, customer service, underwriters, compliance, admins, executives, Otic support |
| Primary channels | WhatsApp, email, web forms, broker, call center, internal staff upload |
| Primary architecture | Next.js frontend, .NET 8 API, Azure SQL, Azure Blob Storage, Azure AI Search, Azure AI Document Intelligence, Azure OpenAI, Microsoft Entra ID |
| Governance rule | AI prepares work. Humans make final regulated insurance decisions. |

---

## 1. Purpose of This Document


---

# July 2026 Update: Chat-First Command Center Data Model Additions

## Data Model Decision

The chat-first Command Center requires persisted, auditable records for command sessions, user messages, assistant responses, structured response blocks, evidence references, and proposed human actions.

These records must be tenant-scoped, workspace-aware, role-aware, and linked to existing claims, documents, approvals, audit events, AI runs, and knowledge sources.

## New Canonical Entities

| Entity | Purpose | MVP Status |
|---|---|---|
| `CommandSession` | A user-facing Command Center conversation session. | MVP |
| `CommandMessage` | User or assistant message in the command conversation. | MVP |
| `CommandResponseBlock` | Typed structured block rendered inside assistant response. | MVP |
| `CommandEvidenceReference` | Evidence used in a command response. | MVP |
| `CommandActionProposal` | Human-reviewable next action proposed by AI. | MVP |
| `CommandMetricSnapshot` | Snapshot of metric values shown in a briefing or answer. | MVP |

## Suggested Table: `CommandSessions`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CommandSessionId | uniqueidentifier | Yes | Primary key. |
| TenantId | uniqueidentifier | Yes | Tenant scope. |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope. |
| UserId | uniqueidentifier | Yes | Session owner. |
| SessionTitle | nvarchar(200) | No | Generated from first user query or briefing. |
| Status | nvarchar(40) | Yes | active, archived, failed. |
| CreatedAt | datetimeoffset | Yes | UTC. |
| UpdatedAt | datetimeoffset | Yes | UTC. |

## Suggested Table: `CommandMessages`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CommandMessageId | uniqueidentifier | Yes | Primary key. |
| CommandSessionId | uniqueidentifier | Yes | FK to CommandSessions. |
| TenantId | uniqueidentifier | Yes | Tenant scope. |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope. |
| ActorType | nvarchar(40) | Yes | user, assistant, system. |
| UserId | uniqueidentifier | No | User actor when applicable. |
| MessageText | nvarchar(max) | No | Safe markdown/text. |
| Intent | nvarchar(120) | No | e.g. show_sla_breaches, summarize_day. |
| AgentRunId | uniqueidentifier | No | FK to AgentRun where AI is used. |
| CorrelationId | nvarchar(120) | Yes | Traceability. |
| CreatedAt | datetimeoffset | Yes | UTC. |

## Suggested Table: `CommandResponseBlocks`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CommandResponseBlockId | uniqueidentifier | Yes | Primary key. |
| CommandMessageId | uniqueidentifier | Yes | FK to assistant message. |
| TenantId | uniqueidentifier | Yes | Tenant scope. |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope. |
| BlockType | nvarchar(80) | Yes | text, metric_pills, claims_table, sla_summary, evidence_list, action_proposal, not_configured, error. |
| SortOrder | int | Yes | Rendering order. |
| PayloadJson | nvarchar(max) | Yes | Validated typed JSON payload. |
| ContainsSensitiveData | bit | Yes | Controls role-safe rendering. |
| CreatedAt | datetimeoffset | Yes | UTC. |

## Suggested Table: `CommandEvidenceReferences`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CommandEvidenceReferenceId | uniqueidentifier | Yes | Primary key. |
| CommandMessageId | uniqueidentifier | Yes | FK to command message. |
| TenantId | uniqueidentifier | Yes | Tenant scope. |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope. |
| EvidenceType | nvarchar(80) | Yes | claim, document, policy_clause, knowledge_chunk, metric, integration_event, audit_event. |
| EvidenceId | nvarchar(200) | Yes | Source ID. |
| SourceSystem | nvarchar(120) | No | Claims DB, SLA service, AI Search, Graph, Document Intelligence. |
| Confidence | decimal(5,4) | No | Optional evidence confidence. |
| DisplayLabel | nvarchar(250) | No | Safe UI label. |
| CreatedAt | datetimeoffset | Yes | UTC. |

## Data Model Guardrails

- Command conversation records must not store raw secrets, tokens, prompts, system messages, or full sensitive provider payloads.
- Response blocks must be validated against known schemas before storage and display.
- Evidence references must enforce tenant/workspace filters.
- Proposed actions are not decisions. They must link to `Approval`, `Task`, or `CustomerUpdateDraft` before execution.
- Command metrics must be snapshots of backend-computed values and must never be hardcoded frontend values.

## Phase 1-4 Data Alignment

| Phase | Data Work |
|---|---|
| Phase 1 | Define response block DTOs and non-persistent local UI models. |
| Phase 2 | Add command sessions/messages/blocks/evidence tables and API DTOs. |
| Phase 3 | Link command messages to AgentRun, AuditEvent, Approval, CustomerUpdateDraft, and KnowledgeQuery records. |
| Phase 4 | Add retention, masking, export, audit review, and security tests for command conversation data. |
This document defines the canonical data model for the **Insurance Intelligence Platform (IIP)**.

It is intended to be used by:

- Backend engineers building the .NET 8 API and database migrations.
- Frontend engineers building IIP screens and API integrations.
- AI engineers building agent tools, RAG flows, extraction pipelines, and review workflows.
- Cloud engineers designing Azure SQL, Blob Storage, AI Search, Service Bus, and monitoring resources.
- Product managers translating PRD/FSD requirements into implementation epics.
- Integration engineers mapping insurer systems, Microsoft Graph, WhatsApp, email, and file imports.
- Security and compliance reviewers validating auditability, role-based access, tenant isolation, and privacy controls.

The document defines:

1. Canonical entities.
2. Entity relationships.
3. Suggested database tables.
4. Required fields and metadata.
5. State/status enumerations.
6. Tenant and workspace scoping rules.
7. Audit and AI traceability requirements.
8. Integration mappings.
9. MVP versus future data scope.
10. Open questions requiring confirmation.

This is not a final physical database schema. It is a canonical specification from which database migrations, API DTOs, integration contracts, and AI tool schemas should be derived.

---

## 2. Core Data Modeling Principles

### 2.1 Tenant First

Every operational record must be scoped to the correct insurer tenant.

Examples:

- Customer
- Policy
- Claim
- Document
- Channel conversation
- Sales lead
- Task
- Approval request
- Audit event
- AI run
- Integration event
- Search index item

No API, agent, search query, export, report, or background job may operate without tenant scope.

### 2.2 Workspace Aware

Within an insurer tenant, IIP should support workspace-level segmentation.

A workspace may represent:

- Branch
- Region
- Business unit
- Product line
- Country
- Broker network
- Claims team
- Customer service queue
- Pilot environment

Every core operational record should include both:

```text
TenantId
WorkspaceId
```

### 2.3 Human Decision Ownership

The data model must clearly separate:

- AI-generated suggestions
- System automation
- Human decisions
- Customer actions
- Admin configuration
- Integration events

A claim can have an AI summary, fraud signal, suggested next action, or extracted field, but a final claim decision must be stored as a human-owned action with decision owner, timestamp, rationale, and audit trail.

### 2.4 Evidence Linked Outputs

Any AI output used in claims, policy, underwriting, customer communication, fraud-signal detection, or compliance review must be linked to evidence.

Evidence may include:

- Uploaded document
- Extracted field
- Policy clause
- Channel message
- Claim timeline event
- Integration event
- Knowledge base source
- Human note

### 2.5 Source System Traceability

IIP should never lose the origin of a record.

Imported records should store:

- Source system
- Source record ID
- Source timestamp
- Import method
- Sync batch ID
- Data confidence
- Last sync status

### 2.6 No Generic Agent Database Access

AI agents must not call generic database tools such as:

```text
query_database(sql)
```

Agents must use scoped, audited tools such as:

```text
get_claim(claimId)
get_claim_documents(claimId)
get_policy_summary(policyId)
get_required_documents(claimType, productLine)
generate_claim_summary(claimId)
create_customer_update_draft(claimId)
```

### 2.7 Immutable Audit by Default

Audit events should be append-only. Corrections should be recorded as new events rather than overwriting history.

### 2.8 Production Must Not Depend on Mock Data

Development can support local mock mode where explicitly configured. Staging and production must not use mock records, fake endpoints, fake AI outputs, or browser-only permissions.

---

## 3. Canonical Data Domains

IIP data should be grouped into the following canonical domains.

| Domain | Purpose | MVP Status |
|---|---|---|
| Tenant and workspace | Insurer setup, workspace scoping, environments | MVP |
| Identity and access | Users, roles, permissions, assignments | MVP |
| Customer 360 | Internal view of policyholders, prospects, brokers, beneficiaries | MVP |
| Policy and coverage | Policies, schedules, product lines, coverage references | MVP basic, deeper integration later |
| Claims | FNOL, claim case, incident, participants, statuses, SLA, review | MVP |
| Documents | Upload, storage, classification, OCR, extraction, evidence | MVP |
| Channels | WhatsApp, email, SMS, webchat, broker, call center inbox | MVP |
| Sales and leads | Lead intake, acquisition pipeline, quote readiness | MVP |
| Tasks and approvals | Work items, review queues, approval gates | MVP |
| AI and evidence | Agent runs, prompts, outputs, confidence, sources | MVP |
| Audit and compliance | Action logs, human overrides, access logs, exports | MVP |
| Integration fabric | Source systems, sync events, write-back requests, failures | MVP foundational |
| Knowledge and search | Policy docs, SOPs, indexed knowledge, citations | MVP basic |
| Reporting and analytics | Operational KPIs, SLA, usage, productivity, ROI | MVP basic |
| Underwriting | Submission parsing, risk summary, referral triggers | Phase 2 |
| Fraud intelligence | Dedicated fraud module, patterns, regulator data | Phase 2 |
| Early warning and retention | Churn/payment/renewal risk intelligence | Phase 2 |
| Payments | Premiums, settlement/payout coordination | Later unless pilot requires |
| Complaints | Complaint handling, escalation, regulatory evidence | Phase 2 or pilot-dependent |

---

## 4. Tenant, Workspace, and Environment Model

### 4.1 Tenant

A tenant represents one insurer, broker, TPA, HMO, bancassurance partner, or enterprise customer using IIP.

Suggested table: `Tenants`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| TenantId | uniqueidentifier | Yes | Primary key |
| TenantCode | nvarchar(80) | Yes | Stable code, e.g. `tenant-demo` |
| DisplayName | nvarchar(200) | Yes | Insurer or institution name |
| LegalName | nvarchar(250) | No | Registered legal name |
| CountryCode | char(2) | Yes | ISO country code, e.g. `UG` |
| DefaultCurrencyCode | char(3) | Yes | e.g. `UGX`, `USD` |
| Status | nvarchar(40) | Yes | active, suspended, pilot, archived |
| DeploymentModel | nvarchar(60) | Yes | SaaS, ManagedApplication, PrivateDeployment, Hybrid |
| DataResidencyRegion | nvarchar(100) | No | Preferred Azure region |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |
| CreatedByUserId | uniqueidentifier | No | Audit metadata |

### 4.2 Workspace

A workspace is a logical partition inside a tenant.

Suggested table: `Workspaces`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| WorkspaceId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | FK to Tenants |
| WorkspaceCode | nvarchar(80) | Yes | e.g. `workspace-claims` |
| DisplayName | nvarchar(200) | Yes | Claims, Motor, Kampala Branch, etc. |
| WorkspaceType | nvarchar(60) | Yes | branch, region, product_line, department, pilot |
| CountryCode | char(2) | No | Optional override |
| CurrencyCode | char(3) | No | Optional override |
| Status | nvarchar(40) | Yes | active, archived |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 4.3 Environment

IIP should distinguish development, staging, demo, and production environments.

Suggested table: `EnvironmentContexts`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| EnvironmentId | uniqueidentifier | Yes | Primary key |
| EnvironmentName | nvarchar(40) | Yes | local, dev, staging, demo, production |
| AllowsMockProviders | bit | Yes | Must be false in staging/production |
| AllowsDemoHeaders | bit | Yes | Must be false in production |
| RequiresEntraAuth | bit | Yes | True for staging/production |
| CreatedAt | datetimeoffset | Yes | UTC |

---

## 5. Identity, Roles, and Permissions

### 5.1 User

Represents an internal insurer staff user, Otic support user, or platform admin.

Suggested table: `Users`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| UserId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Default workspace |
| ExternalIdentityProvider | nvarchar(80) | Yes | EntraID, local-dev, etc. |
| ExternalObjectId | nvarchar(200) | No | Entra object ID |
| Email | nvarchar(320) | Yes | Unique within tenant |
| DisplayName | nvarchar(200) | Yes | User-facing name |
| JobTitle | nvarchar(120) | No | Claims Officer, Supervisor, etc. |
| Department | nvarchar(120) | No | Claims, Compliance, IT |
| Status | nvarchar(40) | Yes | active, invited, disabled, archived |
| LastLoginAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 5.2 Role

Suggested table: `Roles`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| RoleId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| RoleCode | nvarchar(80) | Yes | claims_officer, admin, etc. |
| DisplayName | nvarchar(120) | Yes | Claims Officer |
| Description | nvarchar(500) | No | Role description |
| IsSystemRole | bit | Yes | True for default roles |
| CreatedAt | datetimeoffset | Yes | UTC |

### 5.3 Default Roles

| Role | Description | MVP |
|---|---|---|
| Platform Admin | Otic/support-side setup and managed operations | Yes |
| Insurer Admin | Tenant setup, users, workflows, configuration | Yes |
| Claims Officer | Reviews claims, documents, summaries, missing info | Yes |
| Claims Supervisor | Reviews escalations, approvals, SLA risk | Yes |
| Customer Service Agent | Handles customer inquiries and update drafts | Yes |
| Compliance Officer | Reviews audit logs, decisions, exports | Yes |
| Underwriter | Uses underwriting support workflows | Phase 2/basic placeholder |
| Executive Viewer | Views dashboards, KPIs, ROI | MVP basic |
| IT / Security Admin | Reviews integrations, access, provider health | MVP |

### 5.4 Permission

Suggested table: `Permissions`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| PermissionId | uniqueidentifier | Yes | Primary key |
| PermissionCode | nvarchar(120) | Yes | e.g. `claims.read` |
| Module | nvarchar(80) | Yes | claims, documents, audit, admin |
| Description | nvarchar(300) | No | Human-readable description |

Suggested join tables:

- `UserRoles`
- `RolePermissions`
- `WorkspaceUserAssignments`

### 5.5 Permission Examples

| Permission | Meaning |
|---|---|
| claims.read | View claims within allowed scope |
| claims.create | Create claim intake records |
| claims.assign | Assign or reassign claims |
| claims.escalate | Escalate claim |
| claims.approve_next_step | Approve claim for next workflow step, not final settlement |
| claims.final_decision | Record final human claim decision where allowed |
| documents.upload | Upload documents |
| documents.review_extraction | Review extracted fields |
| documents.approve_field | Approve extracted field |
| customer.read | View Customer 360 |
| channel.read | View channel inbox |
| lead.manage | Manage sales leads |
| audit.read | View audit events |
| admin.manage_users | Manage users and roles |
| integrations.manage | Configure integrations |

---

## 6. Customer 360 Data Model

### 6.1 Customer

Customers are policyholders, prospects, beneficiaries, broker clients, claimants, or insured parties. In V1, Customers is an internal insurer workspace. Customers do not log into the internal app.

Suggested table: `Customers`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CustomerId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Default workspace or branch |
| CustomerType | nvarchar(60) | Yes | individual, company, broker_client, beneficiary, prospect |
| DisplayName | nvarchar(250) | Yes | Person or organization name |
| LegalName | nvarchar(250) | No | Company/registered name |
| PrimaryPhone | nvarchar(40) | No | Use normalized international format where possible |
| PrimaryEmail | nvarchar(320) | No | Optional |
| NationalIdMasked | nvarchar(80) | No | Masked in UI |
| TaxIdentifierMasked | nvarchar(80) | No | Company optional |
| Segment | nvarchar(60) | Yes | retail, sme, corporate, broker_managed, bancassurance, microinsurance |
| LifecycleStatus | nvarchar(60) | Yes | active, prospect, lapsed, dormant, restricted |
| PreferredLanguage | nvarchar(20) | No | English, Luganda, Swahili, etc. |
| PreferredChannel | nvarchar(60) | No | whatsapp, email, sms, call_center |
| ConsentStatus | nvarchar(60) | Yes | complete, partial, missing |
| LastInteractionAt | datetimeoffset | No | Last known interaction |
| SourceSystem | nvarchar(100) | No | Origin system |
| SourceRecordId | nvarchar(200) | No | Origin identifier |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 6.2 Customer Contact Method

Suggested table: `CustomerContactMethods`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ContactMethodId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| CustomerId | uniqueidentifier | Yes | FK to Customers |
| Channel | nvarchar(60) | Yes | phone, whatsapp, email, sms |
| ValueMasked | nvarchar(320) | Yes | Masked where appropriate |
| ValueHash | varbinary | No | For matching without exposing raw PII |
| IsPrimary | bit | Yes | One primary preferred |
| IsVerified | bit | Yes | Verified contact |
| VerifiedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

### 6.3 Consent Record

Suggested table: `ConsentRecords`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ConsentRecordId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| CustomerId | uniqueidentifier | Yes | FK to Customers |
| Purpose | nvarchar(120) | Yes | claims_processing, marketing, service_updates, analytics |
| Status | nvarchar(40) | Yes | granted, revoked, expired, unknown |
| Channel | nvarchar(60) | No | whatsapp, web, email, staff_entry |
| CapturedAt | datetimeoffset | Yes | UTC |
| RevokedAt | datetimeoffset | No | UTC |
| EvidenceDocumentId | uniqueidentifier | No | Consent evidence if available |
| CapturedByUserId | uniqueidentifier | No | Staff user if captured internally |
| SourceSystem | nvarchar(100) | No | Source |

---

## 7. Policy and Product Data Model

### 7.1 Product Line

Suggested table: `ProductLines`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ProductLineId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ProductLineCode | nvarchar(80) | Yes | motor, health, life, property, etc. |
| DisplayName | nvarchar(160) | Yes | Motor Comprehensive |
| Category | nvarchar(80) | Yes | general, life, health, microinsurance |
| Status | nvarchar(40) | Yes | active, inactive |
| CreatedAt | datetimeoffset | Yes | UTC |

### 7.2 Policy

Suggested table: `Policies`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| PolicyId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Workspace/branch/product-line scope |
| CustomerId | uniqueidentifier | Yes | Policyholder/customer |
| ProductLineId | uniqueidentifier | No | FK to ProductLines |
| PolicyNumber | nvarchar(100) | Yes | Insurer policy number |
| PolicyType | nvarchar(100) | No | Comprehensive, third party, etc. |
| Status | nvarchar(60) | Yes | active, expired, cancelled, lapsed, pending |
| EffectiveFrom | date | Yes | Start date |
| EffectiveTo | date | Yes | End date |
| CurrencyCode | char(3) | Yes | UGX, USD, etc. |
| SumInsured | decimal(18,2) | No | Optional |
| PremiumAmount | decimal(18,2) | No | Optional |
| ExcessAmount | decimal(18,2) | No | Optional |
| SourceSystem | nvarchar(100) | No | Core policy system |
| SourceRecordId | nvarchar(200) | No | Origin ID |
| LastSyncedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 7.3 Policy Asset

For motor MVP, policy assets include vehicles.

Suggested table: `PolicyAssets`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| PolicyAssetId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| PolicyId | uniqueidentifier | Yes | FK to Policies |
| AssetType | nvarchar(80) | Yes | vehicle, property, person, business |
| RegistrationNumber | nvarchar(80) | No | Motor vehicle plate |
| Make | nvarchar(100) | No | Vehicle make |
| Model | nvarchar(100) | No | Vehicle model |
| YearOfManufacture | int | No | Vehicle year |
| ChassisNumberMasked | nvarchar(100) | No | Masked if sensitive |
| EngineNumberMasked | nvarchar(100) | No | Masked if sensitive |
| AssetDescription | nvarchar(500) | No | Free text |
| Status | nvarchar(40) | Yes | active, removed, unknown |

### 7.4 Coverage Item

Suggested table: `CoverageItems`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| CoverageItemId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| PolicyId | uniqueidentifier | Yes | FK to Policies |
| CoverageCode | nvarchar(100) | Yes | coverage identifier |
| DisplayName | nvarchar(200) | Yes | Coverage label |
| LimitAmount | decimal(18,2) | No | Optional |
| ExcessAmount | decimal(18,2) | No | Optional |
| TermsReference | nvarchar(300) | No | Link to wording/knowledge item |
| Status | nvarchar(40) | Yes | active, excluded, unknown |

---

## 8. Claims Data Model

### 8.1 Claim

The Claim is the central case object for the MVP.

Suggested table: `Claims`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ClaimId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| ClaimReference | nvarchar(100) | Yes | IIP-generated reference |
| ExternalClaimNumber | nvarchar(100) | No | Insurer/core claim number |
| PolicyId | uniqueidentifier | No | Linked policy where known |
| CustomerId | uniqueidentifier | No | Primary claimant/customer |
| ProductLineId | uniqueidentifier | No | Motor, health, etc. |
| ClaimType | nvarchar(100) | Yes | motor_comprehensive, motor_third_party, etc. |
| ClaimStatus | nvarchar(80) | Yes | See status enum below |
| IncidentDate | datetimeoffset | No | Date/time of loss |
| ReportedAt | datetimeoffset | Yes | FNOL/report timestamp |
| Channel | nvarchar(60) | No | whatsapp, email, web, call_center, broker, staff |
| AssignedToUserId | uniqueidentifier | No | Claims officer |
| SupervisorUserId | uniqueidentifier | No | Supervisor/approval owner |
| Priority | nvarchar(40) | Yes | low, normal, high, urgent |
| RiskLevel | nvarchar(40) | Yes | low, medium, high, critical, unknown |
| CompletenessScore | decimal(5,2) | No | 0-100 |
| AiSummaryStatus | nvarchar(60) | Yes | not_started, processing, ready, failed, needs_review |
| FraudSignalStatus | nvarchar(60) | Yes | not_checked, no_signals, signals_detected, reviewed |
| SlaStatus | nvarchar(60) | Yes | on_track, at_risk, breached, paused |
| CurrencyCode | char(3) | Yes | UGX, USD |
| ClaimedAmount | decimal(18,2) | No | If provided |
| SuggestedReserveAmount | decimal(18,2) | No | Advisory only |
| HumanDecisionStatus | nvarchar(80) | Yes | pending, approved_next_step, rejected, closed, escalated |
| SourceSystem | nvarchar(100) | No | Origin if imported |
| SourceRecordId | nvarchar(200) | No | Origin identifier |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |
| ClosedAt | datetimeoffset | No | UTC |

### 8.2 Claim Status Enum

| Status | Meaning |
|---|---|
| new | Claim has been created but not yet processed |
| intake_started | FNOL details are being captured |
| documents_pending | Required documents not yet received |
| documents_uploaded | Some documents received |
| ai_processing | AI/document pipeline is processing |
| needs_review | Human review required |
| missing_information | Required data/document missing |
| fraud_signal_detected | Advisory fraud/risk signal exists |
| escalated | Supervisor or specialist escalation |
| approved_for_next_workflow_step | Human approved movement to next internal step |
| returned_to_customer | Customer action required |
| exported | Claim pack exported to downstream system |
| closed | Claim closed |
| cancelled | Claim cancelled |

### 8.3 Claim Incident

Suggested table: `ClaimIncidents`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ClaimIncidentId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | Yes | FK to Claims |
| IncidentType | nvarchar(100) | Yes | accident, theft, fire, injury, etc. |
| IncidentDate | datetimeoffset | No | Loss date |
| IncidentLocationText | nvarchar(500) | No | Free text location |
| Latitude | decimal(9,6) | No | Optional |
| Longitude | decimal(9,6) | No | Optional |
| Description | nvarchar(max) | No | Incident description |
| WeatherCondition | nvarchar(100) | No | Optional/future |
| PoliceReportNumber | nvarchar(100) | No | If known |
| CreatedAt | datetimeoffset | Yes | UTC |

### 8.4 Claim Participant

Suggested table: `ClaimParticipants`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ClaimParticipantId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | Yes | FK to Claims |
| CustomerId | uniqueidentifier | No | If linked to Customer |
| ParticipantType | nvarchar(80) | Yes | claimant, insured, driver, third_party, witness, beneficiary |
| DisplayName | nvarchar(250) | Yes | Person/company |
| PhoneMasked | nvarchar(80) | No | Masked contact |
| EmailMasked | nvarchar(320) | No | Masked contact |
| NationalIdMasked | nvarchar(80) | No | Masked identity field |
| IsPrimary | bit | Yes | Primary claimant/insured |
| CreatedAt | datetimeoffset | Yes | UTC |

### 8.5 Claim Timeline Event

Suggested table: `ClaimTimelineEvents`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| TimelineEventId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | Yes | FK to Claims |
| EventType | nvarchar(100) | Yes | document_uploaded, note_added, status_changed, customer_update_sent |
| EventTitle | nvarchar(200) | Yes | Short label |
| EventDescription | nvarchar(max) | No | Details |
| ActorType | nvarchar(60) | Yes | user, system, ai_agent, customer, integration |
| ActorUserId | uniqueidentifier | No | User if applicable |
| OccurredAt | datetimeoffset | Yes | UTC |
| RelatedDocumentId | uniqueidentifier | No | Optional |
| RelatedAiRunId | uniqueidentifier | No | Optional |
| RelatedAuditEventId | uniqueidentifier | No | Optional |

---

## 9. Document Intelligence Data Model

### 9.1 Document

Suggested table: `Documents`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| DocumentId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| CustomerId | uniqueidentifier | No | Optional |
| ClaimId | uniqueidentifier | No | Optional |
| PolicyId | uniqueidentifier | No | Optional |
| LeadId | uniqueidentifier | No | Optional |
| UploadedByUserId | uniqueidentifier | No | Staff uploader |
| UploadedByCustomerChannelId | uniqueidentifier | No | Channel source if customer-sent |
| FileName | nvarchar(260) | Yes | Original file name |
| FileExtension | nvarchar(20) | Yes | pdf, jpg, png, docx, etc. |
| MimeType | nvarchar(120) | Yes | Content type |
| BlobContainer | nvarchar(120) | Yes | Azure Blob container |
| BlobPath | nvarchar(500) | Yes | Storage key/path |
| FileSizeBytes | bigint | Yes | Size |
| Sha256Hash | varbinary(32) | No | Duplicate detection |
| DocumentType | nvarchar(100) | Yes | claim_form, police_report, national_id, etc. |
| ClassificationConfidence | decimal(5,2) | No | 0-100 |
| ProcessingStatus | nvarchar(80) | Yes | uploaded, queued, processing, extracted, needs_review, failed |
| SensitivityLabel | nvarchar(80) | Yes | public, internal, confidential, restricted, pii, claims_evidence |
| IsCustomerVisible | bit | Yes | False for internal evidence |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 9.2 Document Type Enum - Motor Claim MVP

| Document Type | Description | MVP |
|---|---|---|
| claim_form | Customer/insured claim form | Yes |
| police_report | Police accident report | Yes |
| national_id | National identity document | Yes |
| driving_permit | Driver's permit/license | Yes |
| vehicle_photo | Vehicle damage image | Yes |
| repair_quotation | Garage/repair quote | Yes |
| policy_schedule | Policy schedule/certificate | Yes |
| assessor_report | Assessor/loss adjuster report | Strongly recommended |
| receipt | Repair/payment receipt | Optional |
| invoice | Repair/provider invoice | Optional |
| vehicle_logbook | Ownership/registration document | Optional |
| witness_statement | Witness statement | Optional |
| medical_report | Injury-related medical evidence | Optional |
| proof_of_payment | Payment evidence | Optional |
| other | Unclassified or other supporting document | Yes fallback |

### 9.3 Document Version

Suggested table: `DocumentVersions`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| DocumentVersionId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| DocumentId | uniqueidentifier | Yes | FK to Documents |
| VersionNumber | int | Yes | 1, 2, 3 |
| BlobPath | nvarchar(500) | Yes | Versioned storage path |
| ChangeReason | nvarchar(300) | No | Re-upload, corrected scan, redacted copy |
| CreatedAt | datetimeoffset | Yes | UTC |
| CreatedByUserId | uniqueidentifier | No | Staff if applicable |

### 9.4 Document Extraction Job

Suggested table: `DocumentExtractionJobs`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ExtractionJobId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| DocumentId | uniqueidentifier | Yes | FK to Documents |
| Provider | nvarchar(100) | Yes | AzureDocumentIntelligence, mock-local |
| ModelId | nvarchar(200) | No | Custom/prebuilt model |
| JobStatus | nvarchar(80) | Yes | queued, running, succeeded, failed, cancelled |
| StartedAt | datetimeoffset | No | UTC |
| CompletedAt | datetimeoffset | No | UTC |
| ErrorCode | nvarchar(100) | No | Provider/system error |
| ErrorMessage | nvarchar(max) | No | Safe error details |
| RawResultBlobPath | nvarchar(500) | No | Storage for raw extraction JSON |
| CreatedAt | datetimeoffset | Yes | UTC |

### 9.5 Extracted Field

Suggested table: `ExtractedFields`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ExtractedFieldId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| DocumentId | uniqueidentifier | Yes | FK to Documents |
| ExtractionJobId | uniqueidentifier | Yes | FK to DocumentExtractionJobs |
| FieldKey | nvarchar(120) | Yes | e.g. `incident_date` |
| FieldLabel | nvarchar(160) | Yes | Incident Date |
| RawValue | nvarchar(max) | No | Original extracted value |
| NormalizedValue | nvarchar(max) | No | Normalized representation |
| DataType | nvarchar(60) | Yes | string, date, money, number, boolean |
| Confidence | decimal(5,2) | No | 0-100 |
| PageNumber | int | No | Source page |
| BoundingBoxJson | nvarchar(max) | No | Coordinates/evidence reference |
| ReviewStatus | nvarchar(80) | Yes | not_required, pending_review, approved, edited, rejected |
| ReviewedByUserId | uniqueidentifier | No | Human reviewer |
| ReviewedAt | datetimeoffset | No | UTC |
| CorrectedValue | nvarchar(max) | No | Human-corrected value |
| CreatedAt | datetimeoffset | Yes | UTC |

### 9.6 Document Checklist Template

Suggested table: `DocumentChecklistTemplates`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ChecklistTemplateId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant-specific rules |
| ClaimType | nvarchar(100) | Yes | motor_comprehensive, motor_third_party |
| ProductLineCode | nvarchar(80) | Yes | motor |
| RequiredDocumentType | nvarchar(100) | Yes | police_report, claim_form, etc. |
| RequirementLevel | nvarchar(60) | Yes | required, conditional, optional |
| ConditionExpression | nvarchar(max) | No | e.g. injury=true |
| DisplayOrder | int | Yes | UI order |
| IsActive | bit | Yes | Active rule |

### 9.7 Claim Document Requirement

Suggested table: `ClaimDocumentRequirements`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ClaimDocumentRequirementId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | Yes | FK to Claims |
| RequiredDocumentType | nvarchar(100) | Yes | Expected document type |
| RequirementLevel | nvarchar(60) | Yes | required, conditional, optional |
| Status | nvarchar(80) | Yes | missing, uploaded, accepted, rejected, unreadable, expired, mismatched |
| DocumentId | uniqueidentifier | No | Linked uploaded doc |
| LastCheckedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

---

## 10. Channels Data Model

Channels represent customer-originated or partner-originated interaction streams. They feed internal insurer workspaces; customers do not log into the internal app.

### 10.1 Channel Source

Suggested table: `ChannelSources`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ChannelSourceId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ChannelKind | nvarchar(60) | Yes | whatsapp, webchat, email, sms, teams, broker, call_center, website_form |
| DisplayName | nvarchar(160) | Yes | WhatsApp Claims Line |
| HealthStatus | nvarchar(60) | Yes | connected, degraded, disconnected, not_configured |
| ConfigurationStatus | nvarchar(80) | Yes | configured, missing_credentials, disabled |
| LastSyncAt | datetimeoffset | No | UTC |
| FailedEvents | int | Yes | Current error count |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 10.2 Channel Conversation

Suggested table: `ChannelConversations`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ConversationId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| ChannelSourceId | uniqueidentifier | No | FK to ChannelSources |
| CustomerId | uniqueidentifier | No | Matched or created customer |
| CustomerDisplayName | nvarchar(250) | Yes | Display fallback |
| CustomerPrimaryContactMasked | nvarchar(120) | No | Masked contact |
| ChannelKind | nvarchar(60) | Yes | whatsapp, email, broker, etc. |
| Intent | nvarchar(80) | Yes | claim, quote, renewal, complaint, policy_inquiry, document_upload, payment_inquiry, general_service |
| AiConfidence | decimal(5,2) | No | Intent confidence |
| SuggestedRoutingTeam | nvarchar(120) | No | Claims, Sales, Customer Service |
| Status | nvarchar(80) | Yes | new, assigned, in_review, waiting_customer, waiting_officer, converted_to_work_item, closed, escalated |
| AssignedToUserId | uniqueidentifier | No | Internal owner |
| WorkItemId | uniqueidentifier | No | Linked task/work item |
| LastMessagePreview | nvarchar(500) | No | Safe preview |
| LastActivityAt | datetimeoffset | Yes | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

### 10.3 Channel Message

Suggested table: `ChannelMessages`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ChannelMessageId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ConversationId | uniqueidentifier | Yes | FK to ChannelConversations |
| Direction | nvarchar(20) | Yes | inbound, outbound |
| SenderType | nvarchar(60) | Yes | customer, user, system, ai_agent, integration |
| SenderUserId | uniqueidentifier | No | Staff sender |
| MessageText | nvarchar(max) | No | Redacted if necessary |
| MessageTextRedacted | nvarchar(max) | No | Redacted version for audit/reporting |
| MediaDocumentId | uniqueidentifier | No | Uploaded image/pdf/voice note |
| ExternalMessageId | nvarchar(200) | No | Provider message ID |
| DeliveryStatus | nvarchar(60) | Yes | received, sent, delivered, read, failed, queued |
| SentAt | datetimeoffset | No | UTC |
| ReceivedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

### 10.4 Channel Intent Enum

| Intent | Meaning |
|---|---|
| claim | Claim/FNOL initiation or claim follow-up |
| quote | New quote or product inquiry |
| renewal | Renewal inquiry or reminder |
| complaint | Complaint or dissatisfaction |
| policy_inquiry | Policy terms/status question |
| document_upload | Customer sends supporting document |
| payment_inquiry | Premium or claim payment inquiry |
| general_service | Other service request |

---

## 11. Sales and Leads Data Model

Sales and Leads is an internal acquisition workspace fed by external channels. It should not be confused with a customer login area.

### 11.1 Sales Lead

Suggested table: `SalesLeads`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| LeadId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| LeadReference | nvarchar(100) | Yes | IIP lead reference |
| CustomerId | uniqueidentifier | No | Linked customer/prospect |
| CustomerDisplayName | nvarchar(250) | Yes | Display name |
| CustomerPrimaryContactMasked | nvarchar(120) | No | Masked contact |
| Source | nvarchar(80) | Yes | whatsapp, webchat, broker, agent, call_center, email, website_form, bancassurance_partner |
| ProductInterest | nvarchar(80) | Yes | motor, life, health, business, agriculture, travel, property, unknown |
| Status | nvarchar(80) | Yes | new, qualifying, missing_information, quote_in_progress, quote_sent, converted, lost, closed |
| AiConfidence | decimal(5,2) | No | Product/intent confidence |
| ProductFitSummary | nvarchar(max) | No | Advisory summary |
| RequiredInformationCount | int | Yes | Missing info count |
| NextAction | nvarchar(300) | No | Suggested next action |
| AssignedToUserId | uniqueidentifier | No | Staff owner |
| QuoteValue | decimal(18,2) | No | Estimated/potential quote value |
| CurrencyCode | char(3) | Yes | UGX, USD |
| LastActivityAt | datetimeoffset | Yes | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 11.2 Lead Activity

Suggested table: `LeadActivities`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| LeadActivityId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| LeadId | uniqueidentifier | Yes | FK to SalesLeads |
| ActivityType | nvarchar(80) | Yes | note, call, whatsapp, email, quote_generated, document_requested, status_changed |
| Description | nvarchar(max) | No | Details |
| ActorType | nvarchar(60) | Yes | user, system, ai_agent, customer, integration |
| ActorUserId | uniqueidentifier | No | Staff actor |
| OccurredAt | datetimeoffset | Yes | UTC |

### 11.3 Quote Draft

Suggested table: `QuoteDrafts`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| QuoteDraftId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| LeadId | uniqueidentifier | Yes | FK to SalesLeads |
| ProductLineId | uniqueidentifier | No | Product line |
| QuoteStatus | nvarchar(60) | Yes | draft, ready_for_review, sent, expired, converted |
| EstimatedPremium | decimal(18,2) | No | Advisory or rating-engine output |
| CurrencyCode | char(3) | Yes | UGX, USD |
| GeneratedBy | nvarchar(60) | Yes | user, system, ai_agent, rating_engine |
| RequiresHumanReview | bit | Yes | True unless rating flow is approved |
| ReviewedByUserId | uniqueidentifier | No | Staff reviewer |
| ReviewedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

---

## 12. Work Items, Tasks, and Approvals

### 12.1 Work Item

Suggested table: `WorkItems`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| WorkItemId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| WorkItemType | nvarchar(80) | Yes | claim_review, document_review, missing_document, lead_followup, approval, complaint |
| Title | nvarchar(250) | Yes | Work item title |
| Description | nvarchar(max) | No | Details |
| LinkedEntityType | nvarchar(80) | No | claim, document, lead, customer, policy |
| LinkedEntityId | uniqueidentifier | No | Entity identifier |
| Status | nvarchar(80) | Yes | open, assigned, in_progress, blocked, completed, cancelled, escalated |
| Priority | nvarchar(40) | Yes | low, normal, high, urgent |
| AssignedToUserId | uniqueidentifier | No | Owner |
| DueAt | datetimeoffset | No | SLA/due date |
| SlaStatus | nvarchar(60) | Yes | on_track, at_risk, breached, paused |
| CreatedByActorType | nvarchar(60) | Yes | user, system, ai_agent, integration |
| CreatedAt | datetimeoffset | Yes | UTC |
| CompletedAt | datetimeoffset | No | UTC |

### 12.2 Approval Request

Suggested table: `ApprovalRequests`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ApprovalRequestId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| RequestType | nvarchar(100) | Yes | claim_next_step, final_claim_decision, settlement, customer_message, write_back, extraction_override |
| LinkedEntityType | nvarchar(80) | Yes | claim, document, lead, policy, message |
| LinkedEntityId | uniqueidentifier | Yes | Entity ID |
| RequestedByActorType | nvarchar(60) | Yes | user, system, ai_agent |
| RequestedByUserId | uniqueidentifier | No | User if applicable |
| ApproverUserId | uniqueidentifier | No | Required approver |
| Status | nvarchar(60) | Yes | pending, approved, rejected, cancelled, escalated |
| Rationale | nvarchar(max) | No | Human rationale |
| DecisionAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

### 12.3 Approval Boundary Rule

The following actions require human approval:

- Claim approval
- Claim rejection
- Final settlement recommendation becoming a decision
- Payment release
- Fraud determination
- Final underwriting decision
- Policy binding
- Premium change
- Complaint final response
- Customer adverse decision letter
- Write-back to insurer core system where business impact exists

---

## 13. AI, Agent Runs, Evidence, and RAG Data Model

### 13.1 AI Run

Suggested table: `AiRuns`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| AiRunId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Workspace scope |
| AgentCode | nvarchar(100) | Yes | claims_summary_agent, document_checker_agent, policy_qa_agent |
| ToolName | nvarchar(120) | No | If tool call |
| LinkedEntityType | nvarchar(80) | No | claim, document, lead, policy |
| LinkedEntityId | uniqueidentifier | No | Entity ID |
| PromptVersion | nvarchar(80) | No | Prompt registry version |
| ModelProvider | nvarchar(80) | Yes | AzureOpenAI, mock-local |
| ModelDeployment | nvarchar(120) | No | Model deployment name |
| InputTokenCount | int | No | Usage metric |
| OutputTokenCount | int | No | Usage metric |
| Confidence | decimal(5,2) | No | 0-100 |
| Status | nvarchar(80) | Yes | started, succeeded, failed, blocked, escalated |
| OutputSummary | nvarchar(max) | No | Safe output summary |
| OutputBlobPath | nvarchar(500) | No | Full output if large/sensitive |
| RequiresHumanReview | bit | Yes | Governance flag |
| ErrorCode | nvarchar(100) | No | Failure code |
| StartedAt | datetimeoffset | Yes | UTC |
| CompletedAt | datetimeoffset | No | UTC |

### 13.2 AI Evidence Link

Suggested table: `AiEvidenceLinks`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| AiEvidenceLinkId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| AiRunId | uniqueidentifier | Yes | FK to AiRuns |
| EvidenceType | nvarchar(80) | Yes | document, extracted_field, policy_clause, message, audit_event, knowledge_chunk |
| EvidenceEntityId | uniqueidentifier | No | Linked entity ID |
| EvidenceReference | nvarchar(500) | No | Page, clause, chunk ID, etc. |
| RelevanceScore | decimal(6,4) | No | Retrieval score |
| CreatedAt | datetimeoffset | Yes | UTC |

### 13.3 Agent Tool Registry

Suggested table: `AgentTools`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| AgentToolId | uniqueidentifier | Yes | Primary key |
| ToolName | nvarchar(120) | Yes | get_claim, get_claim_documents, etc. |
| Description | nvarchar(500) | Yes | Tool purpose |
| InputSchemaJson | nvarchar(max) | Yes | JSON schema |
| OutputSchemaJson | nvarchar(max) | Yes | JSON schema |
| AuthorizedAgentCodes | nvarchar(max) | Yes | JSON array |
| AuthorizedPermissionCodes | nvarchar(max) | Yes | JSON array |
| TenantScoped | bit | Yes | Must be true for operational tools |
| AuditRequired | bit | Yes | Must usually be true |
| IsActive | bit | Yes | Tool available |
| CreatedAt | datetimeoffset | Yes | UTC |

### 13.4 MVP Agents

| Agent | Data Inputs | Data Outputs |
|---|---|---|
| FNOL Intake Agent | ChannelMessages, Customers, Policies, ProductLines | Claims, ClaimIncidents, ClaimParticipants, WorkItems |
| Document Checker Agent | Documents, ExtractionJobs, ExtractedFields, ChecklistTemplates | ClaimDocumentRequirements, WorkItems, AiRuns |
| Claims Summary Agent | Claims, Documents, ExtractedFields, Policy references, TimelineEvents | AiRuns, AiEvidenceLinks, Claim summary output |
| Fraud Signal Agent | Claims, Documents, ExtractedFields, history, integration data | FraudSignals, WorkItems, AiRuns |
| Policy Intelligence Agent | KnowledgeDocuments, PolicyClauses, Claims/Policies | AiRuns, AiEvidenceLinks, sourced answer |
| Customer Update Agent | Claims, ChannelConversations, Templates | Draft customer message, ApprovalRequest if required |
| Routing Agent | Claim data, lead data, channels, rules | WorkItems, assignments, status transitions |

---

## 14. Audit and Compliance Data Model

### 14.1 Audit Event

Suggested table: `AuditEvents`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| AuditEventId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Workspace scope |
| EventType | nvarchar(120) | Yes | claim_created, document_uploaded, ai_summary_generated, field_approved |
| EntityType | nvarchar(80) | No | claim, document, customer, user, lead |
| EntityId | uniqueidentifier | No | Linked entity |
| ActorType | nvarchar(60) | Yes | user, system, ai_agent, customer, integration |
| ActorUserId | uniqueidentifier | No | User if applicable |
| AgentCode | nvarchar(100) | No | AI agent if applicable |
| Action | nvarchar(120) | Yes | created, updated, approved, rejected, exported, viewed |
| BeforeJson | nvarchar(max) | No | Optional diff before |
| AfterJson | nvarchar(max) | No | Optional diff after |
| Reason | nvarchar(max) | No | Human/system reason |
| CorrelationId | uniqueidentifier | No | Request/trace correlation |
| IpAddressMasked | nvarchar(80) | No | Security metadata |
| UserAgent | nvarchar(500) | No | Optional |
| OccurredAt | datetimeoffset | Yes | UTC |

### 14.2 Required MVP Audit Events

| Event | Trigger |
|---|---|
| user_logged_in | Successful login |
| claim_created | Claim/FNOL created |
| claim_status_changed | Claim status transition |
| claim_assigned | Assignment/reassignment |
| document_uploaded | Document uploaded |
| document_classified | Document type detected |
| field_extracted | Extraction produced field |
| field_edited | Human corrected field |
| field_approved | Human approved field |
| ai_summary_generated | Claim summary created |
| ai_output_viewed | User viewed AI output |
| human_changed_ai_output | User edited/overrode AI output |
| fraud_signal_generated | Advisory signal generated |
| fraud_signal_reviewed | Human reviewed signal |
| missing_document_requested | Customer document request created |
| customer_message_drafted | AI/user generated draft |
| customer_message_approved | Human approved message |
| customer_message_sent | Message sent through channel |
| approval_requested | Approval workflow created |
| approval_decided | Approval approved/rejected |
| export_generated | Claim/evidence export generated |
| integration_event_received | External event received |
| write_back_requested | Controlled outbound update requested |

---

## 15. Integration Fabric Data Model

### 15.1 Connector Configuration

Suggested table: `ConnectorConfigs`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ConnectorConfigId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ConnectorType | nvarchar(80) | Yes | whatsapp, graph_email, policy_admin_api, sftp, csv_import, sms, payment |
| DisplayName | nvarchar(160) | Yes | Human-readable connector name |
| Status | nvarchar(60) | Yes | not_configured, configured, active, disabled, error |
| AuthMethod | nvarchar(80) | Yes | oauth2, managed_identity, api_key, certificate, service_account |
| SecretReference | nvarchar(300) | No | Key Vault reference only, not secret value |
| BaseUrl | nvarchar(500) | No | API endpoint if applicable |
| ReadEnabled | bit | Yes | Allows inbound/read |
| WriteEnabled | bit | Yes | Allows write-back |
| RequiresHumanApprovalForWrite | bit | Yes | Must be true for business-impacting writes |
| LastHealthCheckAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |
| UpdatedAt | datetimeoffset | Yes | UTC |

### 15.2 Integration Event

Suggested table: `IntegrationEvents`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| IntegrationEventId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ConnectorConfigId | uniqueidentifier | No | Connector |
| EventDirection | nvarchar(20) | Yes | inbound, outbound |
| EventType | nvarchar(100) | Yes | message_received, file_uploaded, policy_sync, claim_export |
| SourceSystem | nvarchar(100) | Yes | WhatsApp, Graph, CorePolicy, etc. |
| SourceRecordId | nvarchar(200) | No | External ID |
| TargetEntityType | nvarchar(80) | No | claim, customer, document, lead |
| TargetEntityId | uniqueidentifier | No | Linked entity |
| PayloadBlobPath | nvarchar(500) | No | Payload stored securely |
| ProcessingStatus | nvarchar(80) | Yes | received, queued, processed, failed, ignored, retrying |
| RetryCount | int | Yes | Retry attempts |
| ErrorCode | nvarchar(100) | No | Failure code |
| ErrorMessage | nvarchar(max) | No | Safe error detail |
| CorrelationId | uniqueidentifier | No | Trace ID |
| ReceivedAt | datetimeoffset | Yes | UTC |
| ProcessedAt | datetimeoffset | No | UTC |

### 15.3 Write-Back Request

Suggested table: `WriteBackRequests`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| WriteBackRequestId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ConnectorConfigId | uniqueidentifier | Yes | Destination connector |
| RequestType | nvarchar(100) | Yes | create_claim_note, update_missing_doc_status, export_claim_pack, send_customer_message |
| LinkedEntityType | nvarchar(80) | Yes | claim, document, message, lead |
| LinkedEntityId | uniqueidentifier | Yes | Entity ID |
| PayloadBlobPath | nvarchar(500) | No | Outbound payload |
| Status | nvarchar(80) | Yes | draft, pending_approval, approved, sent, failed, cancelled |
| RequiresHumanApproval | bit | Yes | True for most MVP write-backs |
| ApprovalRequestId | uniqueidentifier | No | Linked approval |
| SentAt | datetimeoffset | No | UTC |
| ErrorCode | nvarchar(100) | No | Failure code |
| CreatedAt | datetimeoffset | Yes | UTC |

### 15.4 MVP Integration Entities

| Integration | Primary Entities |
|---|---|
| WhatsApp Business API | ChannelSources, ChannelConversations, ChannelMessages, Documents, Customers, Claims, SalesLeads |
| Microsoft Graph / Outlook | ConnectorConfigs, IntegrationEvents, Documents, ChannelConversations, ChannelMessages |
| Email attachment ingestion | Documents, DocumentExtractionJobs, ClaimDocumentRequirements |
| Secure file upload | Documents, IntegrationEvents |
| Core policy lookup | Policies, PolicyAssets, CoverageItems, IntegrationEvents |
| Claim pack export | WriteBackRequests, Documents, Claims, AuditEvents |

---

## 16. Knowledge Base and Search Data Model

### 16.1 Knowledge Document

Suggested table: `KnowledgeDocuments`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| KnowledgeDocumentId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | No | Optional workspace scope |
| Title | nvarchar(250) | Yes | Policy wording, SOP, FAQ, etc. |
| DocumentCategory | nvarchar(100) | Yes | policy_wording, claims_sop, underwriting_guideline, faq, checklist |
| ProductLineId | uniqueidentifier | No | Optional |
| BlobPath | nvarchar(500) | Yes | Source file location |
| ApprovalStatus | nvarchar(80) | Yes | draft, approved, archived, rejected |
| ApprovedByUserId | uniqueidentifier | No | Knowledge approver |
| ApprovedAt | datetimeoffset | No | UTC |
| EffectiveFrom | date | No | Optional |
| EffectiveTo | date | No | Optional |
| VersionNumber | int | Yes | Version |
| CreatedAt | datetimeoffset | Yes | UTC |

### 16.2 Knowledge Chunk

Suggested table: `KnowledgeChunks`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| KnowledgeChunkId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| KnowledgeDocumentId | uniqueidentifier | Yes | FK to KnowledgeDocuments |
| ChunkIndex | int | Yes | Order |
| ChunkText | nvarchar(max) | Yes | Text used for retrieval |
| PageNumber | int | No | Source page |
| SectionTitle | nvarchar(250) | No | Heading/clause |
| SearchIndexKey | nvarchar(200) | No | Azure AI Search key |
| EmbeddingVersion | nvarchar(80) | No | Embedding model/version |
| CreatedAt | datetimeoffset | Yes | UTC |

### 16.3 Search Index Item

Suggested table: `SearchIndexRecords`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| SearchIndexRecordId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| IndexName | nvarchar(160) | Yes | Azure AI Search index |
| SourceEntityType | nvarchar(80) | Yes | knowledge_chunk, claim, document, policy |
| SourceEntityId | uniqueidentifier | Yes | Source entity ID |
| IndexedAt | datetimeoffset | Yes | UTC |
| IndexStatus | nvarchar(60) | Yes | indexed, failed, pending, deleted |
| ErrorMessage | nvarchar(max) | No | Error details |

---

## 17. Fraud Signal Data Model

Fraud signals are advisory. IIP must not accuse a customer of fraud. Human investigation is required.

### 17.1 Fraud Signal

Suggested table: `FraudSignals`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| FraudSignalId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | Yes | FK to Claims |
| SignalType | nvarchar(100) | Yes | duplicate_claim, mismatched_dates, inflated_quote, repeated_claimant, document_inconsistency |
| Severity | nvarchar(40) | Yes | low, medium, high, critical |
| Confidence | decimal(5,2) | No | 0-100 |
| Description | nvarchar(max) | Yes | Advisory explanation |
| EvidenceSummary | nvarchar(max) | No | Evidence description |
| AiRunId | uniqueidentifier | No | Linked AI run |
| ReviewStatus | nvarchar(80) | Yes | pending_review, accepted_for_investigation, dismissed, false_positive |
| ReviewedByUserId | uniqueidentifier | No | Human reviewer |
| ReviewedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

### 17.2 Fraud Signal Examples for Motor Claims

| Signal | Data Sources |
|---|---|
| Same vehicle, similar incident date | Claims, PolicyAssets, ClaimIncidents |
| Police report date mismatch | ExtractedFields across Claim Form and Police Report |
| Driver name mismatch | National ID, Driving Permit, Claim Form |
| Inflated repair quote | Repair quotation, assessor report, historical averages |
| Reused document | Document hash, metadata, source file comparison |
| Late reporting | IncidentDate vs ReportedAt |

---

## 18. Underwriting Support Data Model - Phase 2

### 18.1 Underwriting Submission

Suggested table: `UnderwritingSubmissions`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| SubmissionId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| WorkspaceId | uniqueidentifier | Yes | Workspace scope |
| LeadId | uniqueidentifier | No | Linked lead |
| CustomerId | uniqueidentifier | No | Proposed insured |
| ProductLineId | uniqueidentifier | No | Product line |
| SubmissionReference | nvarchar(100) | Yes | IIP reference |
| Status | nvarchar(80) | Yes | new, parsing, missing_info, ready_for_underwriter, quoted, declined_by_human, bound_by_human |
| RiskSummary | nvarchar(max) | No | AI-assisted summary |
| MissingInformationCount | int | Yes | Missing items |
| AiRiskScore | decimal(5,2) | No | Advisory only |
| HumanDecisionStatus | nvarchar(80) | Yes | pending, accepted, declined, referred, withdrawn |
| AssignedUnderwriterUserId | uniqueidentifier | No | Human owner |
| CreatedAt | datetimeoffset | Yes | UTC |

### 18.2 Underwriting Guardrail

The data model must not allow AI-only policy binding or final underwriting acceptance/rejection. Any final status must have:

- HumanDecisionStatus
- AssignedUnderwriterUserId or DecisionUserId
- DecisionAt
- DecisionRationale
- AuditEventId

---

## 19. Early Warning and Retention Data Model - Phase 2

### 19.1 Retention Risk Profile

Suggested table: `RetentionRiskProfiles`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| RetentionRiskProfileId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| CustomerId | uniqueidentifier | Yes | Customer |
| PolicyId | uniqueidentifier | No | Related policy |
| RiskSegment | nvarchar(60) | Yes | loyal, at_risk, high_risk, critical |
| ChurnProbability | decimal(5,2) | No | 0-100 |
| KeyDriversJson | nvarchar(max) | No | Payment issue, claim denied, no engagement, etc. |
| RecommendedAction | nvarchar(300) | No | Advisory action |
| LastScoredAt | datetimeoffset | Yes | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

---

## 20. Payments Data Model - Later / Pilot Dependent

Payments should be treated carefully because they are regulated and high risk. In MVP, IIP may track payment-related status but should not release payments automatically.

### 20.1 Payment Record

Suggested table: `PaymentRecords`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| PaymentRecordId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| ClaimId | uniqueidentifier | No | Claim settlement payment |
| PolicyId | uniqueidentifier | No | Premium payment |
| CustomerId | uniqueidentifier | No | Customer |
| PaymentType | nvarchar(60) | Yes | premium, settlement, refund, fee |
| Amount | decimal(18,2) | Yes | Amount |
| CurrencyCode | char(3) | Yes | UGX |
| Status | nvarchar(80) | Yes | draft, pending_approval, approved, submitted, paid, failed, cancelled |
| PaymentProvider | nvarchar(100) | No | Mobile money, bank, etc. |
| ExternalPaymentReference | nvarchar(200) | No | Provider reference |
| RequiresFinanceApproval | bit | Yes | Must be true for settlement |
| ApprovedByUserId | uniqueidentifier | No | Finance/human approval |
| ApprovedAt | datetimeoffset | No | UTC |
| CreatedAt | datetimeoffset | Yes | UTC |

---

## 21. Complaint Data Model - Phase 2 / Compliance Extension

### 21.1 Complaint Case

Suggested table: `ComplaintCases`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| ComplaintCaseId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| CustomerId | uniqueidentifier | No | Complainant |
| ClaimId | uniqueidentifier | No | Related claim |
| PolicyId | uniqueidentifier | No | Related policy |
| ComplaintReference | nvarchar(100) | Yes | IIP reference |
| Category | nvarchar(100) | Yes | claims_delay, repudiation, service_issue, payment_delay, mis-selling |
| Status | nvarchar(80) | Yes | new, acknowledged, investigating, responded, escalated, closed |
| Severity | nvarchar(40) | Yes | low, medium, high, critical |
| ReceivedAt | datetimeoffset | Yes | UTC |
| DueAt | datetimeoffset | No | SLA date |
| AssignedToUserId | uniqueidentifier | No | Complaint owner |
| FinalResponseApprovedByUserId | uniqueidentifier | No | Human approval |
| ClosedAt | datetimeoffset | No | UTC |

---

## 22. Reporting and Analytics Data Model

Operational reporting can initially use the transactional schema. Over time, IIP should introduce curated reporting views or marts.

### 22.1 Suggested Reporting Views

| View | Purpose |
|---|---|
| vw_ClaimOperationalSummary | Open claims, status, owner, SLA, completeness |
| vw_DocumentProcessingSummary | Documents processed, extraction status, confidence, review status |
| vw_ChannelInboxSummary | Conversations by channel, intent, assignment, status |
| vw_SalesLeadSummary | Leads by source, status, product interest, quote value |
| vw_AiUsageSummary | AI runs, token usage, failures, human review rate |
| vw_AuditActivitySummary | Audit events by actor, entity, action |
| vw_UserWorkloadSummary | Claims/work items by staff user |
| vw_IntegrationHealthSummary | Connector status, failures, retries |

### 22.2 KPI Data Fields

| KPI | Source Entities |
|---|---|
| Open claims | Claims |
| New claims today | Claims |
| Incomplete claims | ClaimDocumentRequirements |
| SLA at risk/breached | Claims, WorkItems |
| Pending approvals | ApprovalRequests |
| Customer update backlog | WorkItems, ChannelConversations |
| AI questions answered | AiRuns |
| Escalations required | Claims, WorkItems |
| Average completeness score | Claims |
| Document extraction review rate | ExtractedFields |
| Claims officer productivity | Claims, WorkItems, AuditEvents |
| Channel health | ChannelSources |
| Integration failures | IntegrationEvents |

---

## 23. Data Classification and Sensitivity

### 23.1 Classification Levels

| Classification | Examples | Controls |
|---|---|---|
| Public | Public product descriptions | Low restriction |
| Internal | Operational notes, workflow configuration | Staff-only |
| Confidential | Claims, policies, internal decisions | RBAC, audit |
| PII | IDs, phone numbers, email, customer identity | Masking, encryption, strict access |
| Sensitive Insurance Evidence | Police reports, medical documents, claim photos | Restricted access, audit, retention |
| Regulated Decision Data | Claim decisions, underwriting decisions, fraud review | Human approval, immutable audit |

### 23.2 PII Handling Rules

- Store raw PII only where necessary.
- Mask PII in UI by default.
- Use hashed values for matching where possible.
- Do not include unnecessary PII in AI prompts.
- Do not train foundation models on customer PII.
- Log access to sensitive records.
- Support retention and deletion policies in later phases.

---

## 24. Multi-Tenant Data Isolation Rules

Every query must apply tenant scoping.

Required query pattern:

```sql
WHERE TenantId = @TenantId
```

Where workspace scoping applies:

```sql
WHERE TenantId = @TenantId
AND WorkspaceId IN (@AuthorizedWorkspaceIds)
```

Agent tools must accept tenant and workspace context implicitly from the authenticated request context, not from arbitrary model-generated input.

No frontend-only authorization is acceptable.

---

## 25. API Mapping to Data Entities

The API Specification should map to this model. MVP endpoints include or imply the following entities.

| Endpoint | Primary Entities |
|---|---|
| GET /health | ProviderHealth, environment status |
| GET /health/live | Runtime status |
| GET /health/ready | Database/provider readiness |
| GET /api/admin/provider-health | ProviderHealth, ConnectorConfigs |
| GET /api/v1/channels/overview | ChannelSources, ChannelConversations, ChannelMessages |
| GET /api/v1/customers/overview | Customers, Policies, Claims, SalesLeads, ConsentRecords |
| GET /api/v1/sales-leads/overview | SalesLeads, LeadActivities, QuoteDrafts |
| POST /api/cases or /api/v1/claims | Claims, ClaimIncidents, ClaimParticipants |
| GET /api/cases or /api/v1/claims | Claims, Customers, Policies |
| GET /api/cases/{caseId} or /api/v1/claims/{claimId} | Claims, Documents, TimelineEvents, AiRuns, AuditEvents |
| POST /api/v1/documents/upload | Documents, DocumentVersions, IntegrationEvents |
| GET /api/v1/documents/{documentId}/extractions | DocumentExtractionJobs, ExtractedFields |
| POST /api/v1/approvals | ApprovalRequests, WorkItems, AuditEvents |
| GET /api/v1/audit | AuditEvents |
| GET /api/v1/integrations/health | ConnectorConfigs, IntegrationEvents |

---

## 26. State and Status Reference

### 26.1 Common Status Fields

| Field | Common Values |
|---|---|
| RecordStatus | active, inactive, archived, deleted |
| ProcessingStatus | queued, processing, succeeded, failed, needs_review |
| ReviewStatus | pending_review, approved, edited, rejected, not_required |
| ApprovalStatus | pending, approved, rejected, cancelled, escalated |
| SlaStatus | on_track, at_risk, breached, paused |
| IntegrationStatus | received, queued, processed, failed, retrying, ignored |
| ChannelHealthStatus | connected, degraded, disconnected, not_configured |

### 26.2 Status Transition Control

Status changes should be validated server-side. Sensitive transitions must create audit events.

Examples:

- `needs_review` to `approved_for_next_workflow_step` requires Claims Officer/Supervisor permission.
- `pending` to `approved` in ApprovalRequests requires approver permission.
- `draft` to `sent` in customer messages requires human approval when message affects claim outcome.
- `signals_detected` to `reviewed` requires fraud/compliance review permission.

---

## 27. Data Validation Rules

### 27.1 Motor Claim MVP Validation Examples

| Field | Rule |
|---|---|
| PolicyNumber | Required if policy lookup is available |
| IncidentDate | Must not be in the future |
| ReportedAt | Must be after or equal to IncidentDate |
| VehicleRegistration | Normalize format, compare across documents |
| DrivingPermitExpiry | Flag if expired before IncidentDate |
| PoliceReportNumber | Required for configured motor claim types |
| RepairQuotationTotal | Must be numeric and currency-aware |
| ClaimantName | Compare across claim form, ID, permit, policyholder |
| DocumentType | Must be one of configured types or `other` |
| CompletenessScore | Derived from required documents and field quality |

### 27.2 Cross-Document Validation Examples

| Validation | Data Sources |
|---|---|
| Incident date consistency | Claim Form, Police Report, Assessor Report |
| Vehicle registration consistency | Claim Form, Photos, Police Report, Policy Schedule |
| Driver identity consistency | National ID, Driving Permit, Claim Form |
| Amount consistency | Repair Quotation, Assessor Report, Invoice |
| Policy period coverage | Policy EffectiveFrom/EffectiveTo vs IncidentDate |

---

## 28. Retention, Deletion, and Archiving

Retention rules must be configurable per tenant and country.

Suggested table: `RetentionPolicies`

| Field | Type | Required | Notes |
|---|---:|---:|---|
| RetentionPolicyId | uniqueidentifier | Yes | Primary key |
| TenantId | uniqueidentifier | Yes | Tenant scope |
| EntityType | nvarchar(80) | Yes | claim, document, audit_event, channel_message |
| RetentionPeriodDays | int | Yes | Number of days |
| ActionAfterRetention | nvarchar(60) | Yes | archive, delete, anonymize, review |
| LegalHoldSupported | bit | Yes | Prevent deletion while on hold |
| IsActive | bit | Yes | Active rule |
| CreatedAt | datetimeoffset | Yes | UTC |

Important rule: Audit and regulated decision records should not be deleted casually. Deletion and anonymization require legal/compliance review.

---

## 29. Physical Storage Recommendations

### 29.1 Azure SQL

Use Azure SQL for structured operational records:

- Tenants
- Users
- Customers
- Policies
- Claims
- Work items
- Approvals
- Extracted fields
- Audit events
- Integration events

### 29.2 Azure Blob Storage

Use Blob Storage for large unstructured objects:

- Uploaded documents
- Claim photos
- Raw extraction JSON
- AI output payloads where large/sensitive
- Exported evidence packs
- Knowledge base source files

### 29.3 Azure AI Search

Use Azure AI Search for:

- Approved policy wordings
- SOPs
- Claims manuals
- Knowledge base chunks
- Searchable document text where allowed
- RAG retrieval with tenant filters

### 29.4 Application Insights and Log Analytics

Use for technical telemetry, not business audit replacement.

Store:

- Request latency
- Exceptions
- Dependency failures
- AI provider errors
- Integration failures
- Correlation IDs

Do not store raw PII in telemetry.

---

## 30. Indexing and Performance Guidance

### 30.1 Required Indexes

| Table | Suggested Index |
|---|---|
| Claims | TenantId, WorkspaceId, ClaimStatus, SlaStatus |
| Claims | TenantId, ClaimReference |
| Documents | TenantId, ClaimId, DocumentType, ProcessingStatus |
| ExtractedFields | TenantId, DocumentId, FieldKey |
| ChannelConversations | TenantId, WorkspaceId, Status, ChannelKind, Intent |
| SalesLeads | TenantId, WorkspaceId, Status, Source, ProductInterest |
| Customers | TenantId, DisplayName |
| Policies | TenantId, PolicyNumber |
| WorkItems | TenantId, AssignedToUserId, Status, DueAt |
| ApprovalRequests | TenantId, Status, ApproverUserId |
| AuditEvents | TenantId, EntityType, EntityId, OccurredAt |
| IntegrationEvents | TenantId, ConnectorConfigId, ProcessingStatus, ReceivedAt |
| AiRuns | TenantId, AgentCode, LinkedEntityType, LinkedEntityId |

### 30.2 Partitioning / Scale Notes

For enterprise deployments with high document and audit volume:

- Consider date-based partitioning for AuditEvents and IntegrationEvents.
- Store large payloads in Blob Storage, not SQL.
- Use async processing for OCR, extraction, indexing, and AI summarization.
- Avoid synchronous AI calls in critical UI page-load paths.
- Use cached summaries where possible, with clear refresh controls.

---

## 31. MVP Data Scope

### 31.1 In Scope for MVP

| Entity | MVP |
|---|---|
| Tenants | Yes |
| Workspaces | Yes |
| Users, Roles, Permissions | Yes |
| Customers | Yes |
| Policies | Basic/read-only or imported |
| ProductLines | Yes |
| Claims | Yes |
| ClaimIncidents | Yes |
| ClaimParticipants | Yes |
| ClaimTimelineEvents | Yes |
| Documents | Yes |
| DocumentExtractionJobs | Yes |
| ExtractedFields | Yes |
| ClaimDocumentRequirements | Yes |
| ChannelSources | Yes |
| ChannelConversations | Yes |
| ChannelMessages | Yes |
| SalesLeads | Yes |
| LeadActivities | Yes |
| WorkItems | Yes |
| ApprovalRequests | Yes |
| AiRuns | Yes |
| AiEvidenceLinks | Yes |
| AuditEvents | Yes |
| ConnectorConfigs | Yes |
| IntegrationEvents | Yes |
| WriteBackRequests | Yes basic |
| KnowledgeDocuments | Yes basic |
| KnowledgeChunks | Yes basic |
| SearchIndexRecords | Yes basic |
| FraudSignals | Yes as advisory/basic |

### 31.2 Phase 2 / Later

| Entity | Timing |
|---|---|
| UnderwritingSubmissions | Phase 2 |
| RetentionRiskProfiles | Phase 2 |
| ComplaintCases | Phase 2 or pilot-dependent |
| PaymentRecords | Later or pilot-dependent |
| Advanced Broker/Agent entities | Phase 2 |
| Regulator export entities | Phase 2/3 |
| Reinsurance reporting entities | Later |
| Voice/call transcript entities | Later |

---

## 32. Migration and Implementation Sequence

Recommended implementation order:

1. Tenant, Workspace, User, Role, Permission tables.
2. Customers and ProductLines.
3. Policies and PolicyAssets basic read/import structure.
4. Claims, ClaimIncidents, ClaimParticipants, ClaimTimelineEvents.
5. Documents, DocumentVersions, DocumentExtractionJobs, ExtractedFields.
6. ClaimDocumentRequirements and ChecklistTemplates.
7. ChannelSources, ChannelConversations, ChannelMessages.
8. SalesLeads and LeadActivities.
9. WorkItems and ApprovalRequests.
10. AiRuns, AiEvidenceLinks, AgentTools.
11. AuditEvents.
12. ConnectorConfigs, IntegrationEvents, WriteBackRequests.
13. KnowledgeDocuments, KnowledgeChunks, SearchIndexRecords.
14. FraudSignals basic advisory model.
15. Reporting views and operational dashboards.
16. Phase 2 modules: Underwriting, Retention, Complaints, Payments.

---

## 33. Seed and Reference Data

MVP seed data should include:

| Category | Examples |
|---|---|
| Product lines | Motor, Health, Life, Property, Agriculture, Travel |
| Claim types | motor_comprehensive, motor_third_party |
| Document types | claim_form, police_report, national_id, driving_permit, vehicle_photo, repair_quotation, policy_schedule |
| Roles | Admin, Claims Officer, Claims Supervisor, Customer Service Agent, Compliance Officer, Executive Viewer |
| Permissions | claims.read, claims.create, documents.upload, audit.read, integrations.manage |
| Channel kinds | whatsapp, webchat, email, sms, broker, call_center, website_form |
| Lead sources | whatsapp, webchat, broker, agent, call_center, email, website_form, bancassurance_partner |
| Work item types | claim_review, document_review, missing_document, lead_followup, approval |
| AI agents | fnol_intake_agent, document_checker_agent, claims_summary_agent, fraud_signal_agent, policy_qa_agent |

---

## 34. Open Questions

| ID | Question | Owner |
|---|---|---|
| DQ-001 | Which real insurer core policy/claims systems must be mapped first? | Engineering / Pilot Customer |
| DQ-002 | Should policies be imported into IIP or queried read-only from source systems during MVP? | Architecture / Product |
| DQ-003 | Which exact WhatsApp provider will be used: Meta direct, Twilio, Infobip, or other? | Engineering |
| DQ-004 | What is the final claims register format required by the pilot insurer/regulator? | Product / Legal |
| DQ-005 | Which Azure region and Azure OpenAI deployment will be used for production? | Cloud Architect |
| DQ-006 | What retention period applies to claim documents, AI runs, and audit events? | Legal / Compliance |
| DQ-007 | What fields must be masked in UI by default? | Security / Compliance |
| DQ-008 | Will settlement/payment tracking be included in pilot scope? | Product / Leadership |
| DQ-009 | Will client/customer portal records be needed in V1, or only WhatsApp/customer channels? | Product |
| DQ-010 | Should FraudSignals be included as MVP basic advisory signals or moved fully to Phase 2? | Product / Claims SME |

---

## 35. Final Data Model Direction

The IIP data model should be treated as a governed insurance operations model built around the following canonical objects:

```text
Tenant
Workspace
User
Role
Customer
Policy
Claim
Document
ChannelConversation
SalesLead
WorkItem
ApprovalRequest
AiRun
AuditEvent
IntegrationEvent
KnowledgeDocument
FraudSignal
```

The MVP should prioritize Motor Claims and Document Intelligence while leaving enough structure for customer acquisition, underwriting support, fraud intelligence, early warning, policy servicing, complaints, payments, and Marketplace/private deployment readiness.

The data model must preserve the central IIP principle:

> AI prepares insurance work. Humans make consequential insurance decisions.
