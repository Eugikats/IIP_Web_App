# Integration Implementation Playbook

| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Integration Implementation Playbook |
| Version | 1.0 |
| Status | Draft |
| Audience | Integration engineers, backend engineers, solution architects, DevOps engineers, support engineers, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This playbook converts the IIP System Integration Strategy into connector-by-connector implementation steps for WhatsApp, Microsoft Graph, email, Blob, AI services, policy systems, claims systems, and future systems.


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


## Integration Principles

- Every provider must expose configured, Not configured, degraded, and error states.
- Integrations should be asynchronous where latency, retries, or external reliability risk exists.
- Integration payloads must include correlation IDs, tenant/workspace context, source provider, and timestamps.
- External callbacks/events must be idempotent.
- No integration should bypass human approval for consequential insurance actions.

## Connector Implementation Matrix

| Connector | MVP Role | Implementation Steps | Key Risks |
| --- | --- | --- | --- |
| WhatsApp Business API | Primary customer intake/update channel | Configure provider; receive webhook; validate signature; normalize messages; link customer/claim; queue processing; draft replies for review | Provider onboarding, message consent, retry handling. |
| Microsoft Graph / Outlook | Email intake and staff communication context | Register app; configure scopes; read permitted mailboxes; capture relevant messages; link to customer/claim; audit access | Scopes, tenant consent, mailbox permissions. |
| Azure Blob Storage | Claim document storage | Upload with tenant/workspace path; store metadata; secure access; link to document record; scan/validate file | Access control, retention, file type risks. |
| Azure AI Document Intelligence | OCR and extraction | Submit document; capture classification/extraction; store confidence; route low confidence to human review | Document variability, confidence threshold. |
| Azure OpenAI | Summaries, drafts, risk signals | Use tool registry; ground on approved sources; store AgentRun and output; enforce review gates | Hallucination, prompt injection, unsafe advice. |
| Azure AI Search | RAG over knowledge/documents | Create indexes; tenant filters; chunk approved content; retrieve citations; log sources | Cross-tenant retrieval, stale content. |
| Policy admin system | Read-only policy lookup | Define API/export path; map policy number; cache reference data; show source freshness | Legacy integration, data quality. |
| Claims system | Future sync/export | Define push or export pack; avoid duplicate decisions; audit handoff | Write-back risk, ownership. |
| Power BI/Fabric | Future analytics | Publish curated views; govern access; protect tenant data | Data leakage, licensing. |

## Generic Connector Pattern

| Step | Action | Required Evidence |
| --- | --- | --- |
| 1 | Register provider configuration | Provider appears in Admin/Support with Not configured until valid. |
| 2 | Validate credentials/secrets via Key Vault | No secrets exposed in app or logs. |
| 3 | Implement health check | Provider health response includes status, last checked, error category. |
| 4 | Normalize inbound/outbound payloads | Internal canonical event format is used. |
| 5 | Queue long-running work | Retries and dead-letter behavior are defined. |
| 6 | Emit audit/integration events | Support can trace event by correlation ID. |
| 7 | Test failure modes | Timeout, invalid credential, provider unavailable, duplicate event. |

## Provider Health States

| State | Meaning | UI / Support Behavior |
| --- | --- | --- |
| Configured | Provider has valid config and passed recent check | Show healthy status and last check. |
| Not configured | Required config is missing | Show admin action required; do not show fake data. |
| Degraded | Provider reachable but slow/partial/errors above threshold | Warn users and support; allow safe retry. |
| Error | Provider unavailable or credentials invalid | Block dependent actions where needed; show correlation ID. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-INT-001 | Every MVP connector has provider health and Not configured states. | Provider health QA. |
| AC-INT-002 | Inbound channel events are idempotent and auditable. | Integration test. |
| AC-INT-003 | AI and document integrations store source, confidence, and audit records. | AI/document QA. |
| AC-INT-004 | No integration performs final claims decision without human action. | Workflow review. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-INT-001 | Which WhatsApp provider or BSP will be used? | Affects API/webhook implementation. |
| OQ-INT-002 | Which Microsoft Graph scopes are approved for pilot? | Affects security review. |
| OQ-INT-003 | Will MVP connect to a live policy system or use controlled import/reference data? | Affects integration scope. |
| OQ-INT-004 | What systems must receive export packs after claim review? | Affects workflow handoff. |
