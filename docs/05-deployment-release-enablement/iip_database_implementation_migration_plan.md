# Database Implementation & Migration Plan

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Database Implementation & Migration Plan |
| Version | 1.0 |
| Status | Draft |
| Audience | Backend engineers, database engineers, architects, DevOps engineers, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This document converts the IIP Data Model into EF Core entities, SQL tables, indexes, constraints, migrations, seed data, rollout sequence, and database quality controls.


This document uses the existing IIP project context and prior documentation pack: Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, UI/UX Handoff, API Specification, Data Model, Security & Compliance Document, QA/Test Plan, Deployment Plan, User/Admin Guide, Implementation Roadmap, and Strategy/Requirements/Roadmap Pack.

Confirmed project baseline:
- Official product name: Insurance Intelligence Platform (IIP).
- IIP is an Azure-native, human-reviewed insurance operations platform, not a replacement for core insurance systems.
- MVP focus: motor claims and document intelligence for a mid-to-large insurer pilot.
- Customer engagement is WhatsApp-first and channel-led; customers do not log into the internal staff application.
- Internal modules include Command Center, Channels, Customers, Sales & Leads, Claims, Documents, Knowledge Base, Approvals, Compliance & Audit, Reports & Analytics, Integrations, Admin Settings, and Support & Health.
- The internal UI is a role-aware staff workspace with persistent navigation, top bar, global search, operational states, and contextual Copilot panel.
- The platform uses Next.js, React, TypeScript, and Tailwind on the frontend and .NET 8 / ASP.NET Core on the backend.
- Azure services include Azure SQL, Blob Storage, Key Vault, Azure AI Search, Azure OpenAI, Azure AI Document Intelligence, Service Bus/Event Grid, Functions/Durable Functions, API Management, Application Insights/Log Analytics, Entra ID/MSAL, and optional Managed Application packaging.
- AI assists with extraction, summarization, classification, routing, drafting, risk signals, and evidence preparation; humans make regulated or consequential decisions.
- Staging and production must not use fake operational data. Empty states, error states, and Not configured states must be real and explicit.


## Database Principles

- Every tenant-scoped table must include TenantId.
- Every workspace-scoped operational table should include WorkspaceId.
- Claims, documents, AI outputs, approvals, channel events, and audit events must be traceable.
- Use EF Core migrations for schema changes.
- Do not manually alter production schema outside approved migration/runbook process.
- Seed data must be environment-specific and must not masquerade as production data.

## Core Entity Implementation Map

| Entity | Important Fields | Purpose |
| --- | --- | --- |
| Tenant | TenantId, Name, Status, Region, CreatedAt | Root isolation entity. |
| Workspace | WorkspaceId, TenantId, Name, Type, Status | Operational workspace context. |
| User | UserId, TenantId, DisplayName, Email, ExternalIdentityId, Status | Internal staff users. |
| Role | RoleId, TenantId, Name | Role definitions. |
| UserRole | UserId, RoleId, WorkspaceId | Role assignment. |
| Customer | CustomerId, TenantId, WorkspaceId, Name, Contact, ConsentStatus | Customer context. |
| Policy | PolicyId, TenantId, CustomerId, PolicyNumber, ProductLine, Status | Policy reference. |
| Claim | ClaimId, TenantId, WorkspaceId, CustomerId, PolicyId, ClaimNumber, Status, SLAStatus | Core motor claim. |
| Document | DocumentId, TenantId, WorkspaceId, ClaimId, BlobUri, DocumentType, Status | Uploaded document. |
| DocumentExtraction | ExtractionId, DocumentId, FieldsJson, Confidence, ReviewStatus | Extraction result. |
| ChannelConversation | ConversationId, TenantId, WorkspaceId, CustomerId, Channel, Status | WhatsApp/email context. |
| Approval | ApprovalId, TenantId, WorkspaceId, EntityType, EntityId, Status, DecisionBy | Human review gate. |
| Task | TaskId, TenantId, WorkspaceId, AssignedTo, Status, DueAt | Operational work item. |
| AuditEvent | AuditEventId, TenantId, WorkspaceId, ActorId, Action, EntityType, EntityId, Timestamp | Immutable audit trail. |
| AIInteraction | AIInteractionId, TenantId, WorkspaceId, AgentRunId, Output, Confidence, Sources | AI output record. |
| AgentRun | AgentRunId, TenantId, WorkspaceId, AgentName, Status, StartedAt, CompletedAt | AI execution record. |
| IntegrationEvent | IntegrationEventId, TenantId, Provider, EventType, Status, CorrelationId | Integration trace. |

## Indexing Guidance

| Table | Recommended Indexes | Reason |
| --- | --- | --- |
| Claim | TenantId+WorkspaceId+Status; TenantId+ClaimNumber; TenantId+CustomerId; TenantId+SLAStatus | Claims inbox filters and lookup. |
| Document | TenantId+WorkspaceId+ClaimId; TenantId+DocumentType+Status | Document queue and claim detail. |
| AuditEvent | TenantId+WorkspaceId+EntityType+EntityId+Timestamp; TenantId+ActorId+Timestamp | Evidence trail. |
| AIInteraction | TenantId+WorkspaceId+AgentRunId; TenantId+CreatedAt | AI review and audit. |
| ChannelConversation | TenantId+WorkspaceId+CustomerId; TenantId+Channel+Status | Customer/channel context. |
| IntegrationEvent | TenantId+Provider+Status+CreatedAt; CorrelationId | Diagnostics and retries. |

## Migration Sequence

| Step | Migration Theme | Notes |
| --- | --- | --- |
| M001 | Tenant, Workspace, User, Role, UserRole | Foundation. |
| M002 | Customer, Policy, Claim | Core claims. |
| M003 | Document and DocumentExtraction | Document intelligence. |
| M004 | ChannelConversation and messages | Channel context. |
| M005 | Task and Approval | Human review workflow. |
| M006 | AuditEvent, AgentRun, AIInteraction | Audit and AI governance. |
| M007 | IntegrationEvent and ProviderStatus | Operational diagnostics. |
| M008 | Reports/materialized views if needed | Pilot metrics. |

## Data Integrity Rules

| Rule | Database Control |
| --- | --- |
| No orphan claims | Claim must reference Tenant and Workspace; Customer/Policy optional depending intake stage. |
| No orphan documents | Document must reference Tenant and Workspace and should reference Claim unless in intake staging. |
| Immutable audit events | AuditEvent updates should be prohibited except controlled retention/legal process. |
| AI output traceability | AIInteraction must reference AgentRun and source context where available. |
| Tenant-bound rows | TenantId required on all tenant data tables. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-DB-001 | EF Core migrations can create schema from empty database. | Migration pipeline test. |
| AC-DB-002 | Tenant boundary tests pass for claims, documents, audit, AI, and channel records. | Integration test. |
| AC-DB-003 | Audit events cannot be silently modified by normal application paths. | Security review. |
| AC-DB-004 | Indexes support claims inbox and audit queries within pilot performance targets. | Performance test. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-DB-001 | Should pilot use shared database, per-tenant database, or per-customer deployment? | Affects schema and operations. |
| OQ-DB-002 | What retention periods apply to claims, documents, AI outputs, and audit logs? | Affects database lifecycle. |
| OQ-DB-003 | What exact fields are required for motor claim intake in the first insurer pilot? | Affects Claim schema. |
