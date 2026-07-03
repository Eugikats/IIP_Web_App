# Backend Implementation Guide - Managed Application

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Backend Implementation Guide - Managed Application |
| Version | 1.0 |
| Status | Draft |
| Audience | Backend engineers, solution architects, DevOps engineers, security leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines how the .NET 8 backend should be implemented for customer-tenant or Azure Managed Application deployment scenarios.


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


## Managed Application Positioning

The Managed Application path is intended for enterprise insurers that require IIP resources to run inside, or under the control of, the customer Azure tenant. This path emphasizes customer data boundaries, managed identities, private configuration, deployment repeatability, and support access governance.

## Backend Service Responsibilities

| Area | Implementation Guidance | Notes |
| --- | --- | --- |
| API host | ASP.NET Core .NET 8 service | Prefer clean layering: API, Application, Domain, Infrastructure. |
| Tenant context | Resolve tenant/workspace from validated identity and headers | Never trust client-only tenant claims without server validation. |
| Persistence | Azure SQL via EF Core | Use migrations; avoid direct schema edits. |
| Storage | Azure Blob Storage with managed identity | Documents must be linked to claim/customer and audit records. |
| Secrets | Azure Key Vault | No secrets in appsettings, repo, logs, or prompts. |
| AI services | Azure OpenAI, Document Intelligence, AI Search | All AI calls must create AgentRun/AIInteraction records. |
| Messaging | Service Bus/Event Grid where asynchronous work is needed | Document processing and outbound channel work should be queued. |
| Observability | Application Insights and structured logs | Include correlation ID and tenant/workspace context. |
| API Management | Recommended front door for customer-facing enterprise deployment | Use policy controls, throttling, and diagnostics. |

## Recommended Backend Project Structure

| Project | Responsibility |
| --- | --- |
| Iip.Api | Controllers/minimal APIs, auth filters, request validation, response shaping. |
| Iip.Application | Use cases, commands, queries, DTOs, validators, orchestration. |
| Iip.Domain | Entities, value objects, domain services, enums, business rules. |
| Iip.Infrastructure | EF Core, Blob, Key Vault, AI Search, Azure OpenAI, Graph, WhatsApp, queues. |
| Iip.Workers | Background jobs for document processing, indexing, retries, notifications. |
| Iip.Tests | Unit, integration, API contract, security boundary tests. |

## Managed Application Boundaries

| Boundary | Rule |
| --- | --- |
| Customer data | Customer claim, customer, document, audit, and AI output data must remain in the agreed deployment boundary. |
| Publisher support | Support access must be explicit, least-privilege, time-bounded where possible, and auditable. |
| Configuration | Customer-specific settings must be stored in Key Vault/database configuration, not code. |
| Networking | Private endpoints/VNet integration should be considered for production customer deployments. |
| Deployment | Infrastructure must be reproducible through IaC and release pipeline gates. |

## API Implementation Rules

| Rule | Requirement |
| --- | --- |
| Authorization | All write and sensitive read operations require server-side authorization. |
| Validation | Requests must validate tenant, workspace, role, input shape, file type, and file size. |
| Errors | Return consistent problem details without leaking secrets or internal stack traces. |
| Idempotency | Use idempotency keys for document intake, channel events, and integration callbacks where needed. |
| Auditing | Claims, documents, approvals, AI outputs, messages, exports, and admin changes must emit audit events. |

## Initial Endpoint Groups

| Group | Known / Expected Endpoints | Status |
| --- | --- | --- |
| Health | GET /health, /health/live, /health/ready | Known baseline. |
| Provider health | GET /api/admin/provider-health | Known baseline. |
| Cases | POST /api/cases, GET /api/cases, GET /api/cases/{caseId} | Known baseline. |
| Channels | GET /api/v1/channels/overview | Expected from implementation notes. |
| Customers | GET /api/v1/customers/overview | Expected from implementation notes. |
| Sales & Leads | GET /api/v1/sales-leads/overview | Expected from implementation notes. |
| Documents | Upload, classify, extract, review endpoints | Requires Confirmation. |
| AI agent runs | Create/read AgentRun and AIInteraction records | Requires Confirmation. |
| Approvals | Create/list/respond to approval tasks | Requires Confirmation. |
| Audit | Query audit events and evidence trail | Requires Confirmation. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-MA-001 | Backend enforces tenant/workspace/role authorization server-side. | Security test evidence. |
| AC-MA-002 | Managed identities and Key Vault are used for sensitive configuration. | Deployment review. |
| AC-MA-003 | All consequential actions produce audit records. | Audit query evidence. |
| AC-MA-004 | Provider health exposes configured, Not configured, degraded, and error states. | Provider health test. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-MA-001 | Will the first pilot use Azure Managed Application or Otic-hosted SaaS? | Affects deployment packaging. |
| OQ-MA-002 | What customer-tenant support access model is acceptable? | Affects operations and security. |
| OQ-MA-003 | Which Azure region and data residency requirements apply? | Affects architecture and compliance. |
