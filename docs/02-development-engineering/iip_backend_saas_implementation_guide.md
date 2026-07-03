# Backend Implementation Guide - SaaS

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Backend Implementation Guide - SaaS |
| Version | 1.0 |
| Status | Draft |
| Audience | Backend engineers, SaaS architects, DevOps engineers, security leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines how the .NET 8 backend should support the Otic-hosted SaaS path, including multi-tenant hosting, tenant isolation, shared services, provisioning, and operations.


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


## SaaS Positioning

The SaaS path supports fast demos, controlled pilots, smaller insurers, and future multi-client operations. It must not weaken tenant isolation, auditability, or human-reviewed AI governance.

## SaaS Tenancy Model

| Layer | Recommended Approach | Reason |
| --- | --- | --- |
| Identity | Entra ID / MSAL with tenant/workspace mapping | Enterprise-compatible authentication. |
| Application | Single deployed backend with tenant-aware services | Efficient shared operations. |
| Data | Azure SQL with strong TenantId partitioning; consider per-tenant database for larger clients | Balance cost, isolation, and growth. |
| Storage | Blob containers or prefixes separated by tenant | Simplifies lifecycle and access policy. |
| Search | Tenant-filtered indexes or per-tenant indexes where needed | Prevents cross-tenant retrieval. |
| AI logs | Tenant-bound AgentRun and AIInteraction records | Supports audit and model quality review. |

## Tenant Isolation Rules

| Rule | Implementation Requirement |
| --- | --- |
| Every tenant-scoped table has TenantId | EF Core global query filters or explicit filters are mandatory. |
| Every workspace-scoped record has WorkspaceId | Workspace context is enforced for claims operations. |
| No cross-tenant joins by default | Only Otic support/admin paths with explicit authorization may view across tenants. |
| Blob paths include tenant/workspace identifiers | Storage access policies must prevent accidental leakage. |
| AI retrieval filters by tenant/workspace | RAG must not retrieve another tenant knowledge base or document. |
| Support access is audited | Otic Admin and Support actions must be traceable. |

## SaaS Provisioning Flow

| Step | Action | Output |
| --- | --- | --- |
| 1 | Create Tenant record | TenantId and default settings. |
| 2 | Create Workspace | Workspace for claims MVP, e.g. workspace-claims. |
| 3 | Configure roles and initial admin | Admin access for client or Otic setup user. |
| 4 | Configure providers | WhatsApp/email/AI/search/document intelligence states. |
| 5 | Create storage and search partition | Blob container/prefix and search index/filter config. |
| 6 | Load seed/demo data only if explicitly demo environment | No fake data in staging/production. |
| 7 | Run validation | Health, auth, RBAC, tenant boundary, provider health. |

## Shared Services

| Service | SaaS Guidance |
| --- | --- |
| API Management | Use for routing, throttling, diagnostics, and versioning. |
| Application Insights | Use tenant-aware structured logs without exposing sensitive values. |
| Key Vault | Store shared and tenant-specific secrets separately where practical. |
| Service Bus | Use tenant/workspace metadata and idempotency for async workloads. |
| Billing hooks | Track usage events for future pricing: documents processed, AI runs, messages, seats. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-SAAS-001 | Tenant boundary tests prove data cannot leak between tenants. | Automated API/integration test evidence. |
| AC-SAAS-002 | Provisioning creates tenant, workspace, roles, provider states, and validation report. | Provisioning checklist. |
| AC-SAAS-003 | Support and Otic Admin actions are audited. | Audit record review. |
| AC-SAAS-004 | SaaS deployment never treats mock data as real production data. | Environment data-state review. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-SAAS-001 | Will early pilots use shared database or per-tenant database? | Affects cost and isolation. |
| OQ-SAAS-002 | What billing/usage metering fields are required for first commercial launch? | Affects schema and events. |
| OQ-SAAS-003 | Which support tooling will track SaaS tenant issues? | Affects operating model. |
