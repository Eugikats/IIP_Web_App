# Frontend Implementation Guide

> **VISUAL GUIDANCE SUPERSEDED — 2026-09-08.** Palette, typography, spacing, components, shell chrome, and navigation structure in this document are replaced by the approved design language. Start at [`DESIGN.md`](../../DESIGN.md), then [`iip_design_language_v2.md`](../03-design-architecture/iip_design_language_v2.md) and [`iip_ux_cognitive_load_and_navigation_model.md`](../03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md). This document remains valid for API-to-UI contracts, data flow, role behaviour, and screen responsibilities.

---

## July 2026 Update - Chat-First Command Center Direction

This version updates the document to align with the approved **chat-first Command Center** direction for **IIP Operations**.

Authoritative design decision:

> The Command Center is not a dashboard with chat added to it. The Command Center is a **chat-first operational command workspace** where metrics, claims, documents, evidence, approvals, actions, and reports appear as structured blocks inside the conversation.

The implementation must preserve the IIP production rules:

- no fake operational records, metrics, mailboxes, users, or provider states in deployed environments;
- all operational answers must be tenant-scoped, role-aware, source-grounded, and auditable;
- the AI may prepare, summarize, classify, recommend, route, and draft, but humans remain responsible for consequential insurance decisions;
- unavailable or unconfigured capabilities must show **`Not configured`** rather than simulated success;
- the approved Light Intelligence Workspace visual system remains in force.

---

## Chat-First Command Center Frontend Implementation Update

### Required implementation direction

The Command Center route must be rebuilt as a chat-first operational workspace. The center column is the primary interaction area and should occupy the largest available area between the left sidebar and right evidence rail.

Do not implement the Command Center as a conventional dashboard grid with a small chat panel. Dashboard information must be rendered as structured blocks inside the conversation.

### Recommended route and component structure

```text
app/(app)/command-center/page.tsx
components/command-center/
  CommandCenterChatPage.tsx
  CommandChatWorkspace.tsx
  CommandChatThread.tsx
  CommandMessage.tsx
  CommandComposer.tsx
  MetricPillStrip.tsx
  SuggestedPromptGrid.tsx
  EmbeddedResultBlock.tsx
  ClaimsResultTable.tsx
  SlaRiskBlock.tsx
  DocumentGapBlock.tsx
  ApprovalQueueBlock.tsx
  DraftCommunicationBlock.tsx
  ManagementSummaryBlock.tsx
  EvidenceRail.tsx
  CommandEmptyState.tsx
  CommandErrorState.tsx
  CommandNotConfiguredState.tsx
```

### Command Center first-load behavior

On initial load, the page should request the real daily briefing and render it as the first assistant message.

```text
GET /api/v1/command-center/briefing
GET /api/v1/command-center/metrics
```

If the backend is unavailable, unauthorized, or not configured, render explicit states. Do not use fallback demo metrics.

### Chat submission behavior

The command composer should submit natural-language commands to the backend:

```text
POST /api/v1/command-center/query
```

The backend response should return typed blocks. The frontend should render only known block types and show a safe unsupported-block state for unknown types.

### Recommended response rendering model

```ts
type CommandCenterBlock =
  | { type: "text"; markdown: string }
  | { type: "metric_pills"; items: MetricPill[] }
  | { type: "claims_table"; rows: ClaimAttentionRow[]; filters?: Record<string, string> }
  | { type: "sla_summary"; items: SlaRiskItem[] }
  | { type: "document_gap_list"; items: DocumentGapItem[] }
  | { type: "approval_queue"; items: ApprovalQueueItem[] }
  | { type: "draft_communication"; draftId: string; preview: string; requiresApproval: boolean }
  | { type: "management_summary"; title: string; sections: SummarySection[] }
  | { type: "evidence_list"; sources: EvidenceSource[] }
  | { type: "action_buttons"; actions: CommandAction[] };
```

### Frontend safety requirements

- Never render operational metrics from local constants in deployed runtime.
- Never let browser-selected role/workspace values authorize data access.
- Never render prompts, system messages, tokens, provider exceptions, internal fraud/risk notes, reserves, or secrets in the chat or evidence rail.
- Render untrusted content safely; do not use unsafe HTML insertion for email, document, customer, or AI text.
- Every actionable button must call a real backend endpoint or show `Not configured` / unavailable.
- Customer communications drafted in the chat must clearly show whether human approval is required.

### Phase mapping

| Phase | Frontend scope |
|---|---|
| Phase 1 | Build chat-first shell, sticky composer, daily briefing placeholder state connected to real empty/loading/error contracts. |
| Phase 2 | Connect metrics, SLA, claims, documents, approvals, and provider-health blocks to real backend endpoints. |
| Phase 3 | Add command actions: prepare customer update, create approval task, generate management summary, open filtered queues. |
| Phase 4 | Add visual QA, accessibility, responsive behavior, no-fake-data checks, security rendering checks, and production hardening. |


| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Frontend Implementation Guide |
| Version | 1.1 - Chat-First Command Center Update |
| Status | Draft - updated for chat-first Command Center |
| Audience | Frontend engineers, UI developers, UX designers, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This guide defines frontend routes, layouts, shared components, API hooks, state handling, RBAC behavior, screen implementation order, and UI quality gates for IIP.


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


## Frontend Principles

- Build a professional enterprise staff app, not a consumer portal.
- Customers do not log into the internal app.
- Every module must render loading, empty, error, unauthorized, and Not configured states where applicable.
- Role-aware navigation is a UI convenience only; backend authorization remains mandatory.
- The Command Center should feel like a modern operational command workspace with a contextual Copilot, not a static dashboard only.

## Recommended Route Map

| Route | Module | Purpose | MVP Priority |
| --- | --- | --- | --- |
| / | Command Center | Operational home, metrics, quick links, Copilot context | Critical |
| /claims | Claims | Claims inbox, filters, search, SLA/risk badges | Critical |
| /claims/[caseId] | Claims | Claim review, timeline, documents, AI summary, approvals | Critical |
| /documents | Documents | Document queue, extraction review, missing document status | Critical |
| /channels | Channels | WhatsApp/email conversations and intake context | High |
| /customers | Customers | Customer overview and related claims/policies/conversations | High |
| /sales-leads | Sales & Leads | Lead overview and customer acquisition context | Medium |
| /approvals | Approvals | Human review queue and escalation approvals | High |
| /audit | Compliance & Audit | Audit trail, AI interaction history, export evidence | High |
| /reports | Reports & Analytics | Pilot metrics and operational reports | Medium |
| /integrations | Integrations | Provider status and configuration state | High |
| /admin | Admin Settings | Workspace, roles, checklists, templates, settings | High |
| /support-health | Support & Health | Provider health, diagnostics, queue status | High |

## Screen Implementation Order

| Order | Screen / Area | Why First |
| --- | --- | --- |
| 1 | App shell, nav, top bar, role/workspace display | All modules depend on this. |
| 2 | Provider health and data-state components | Prevents fake success states. |
| 3 | Claims inbox and case detail | Core MVP workflow. |
| 4 | Document queue and review panel | Core MVP value. |
| 5 | Copilot panel and AI output cards | AI assistance layer. |
| 6 | Approvals and audit trail | Human governance and trust. |
| 7 | Channels/customers/sales overviews | Context and acquisition expansion. |
| 8 | Reports/admin/support hardening | Pilot readiness. |

## API Hook Pattern

| Hook | Endpoint / Source | Expected UI States |
| --- | --- | --- |
| useProviderHealth | /api/admin/provider-health | loading, configured, Not configured, degraded, error |
| useCases | GET /api/cases | loading, empty, table, error, unauthorized |
| useCaseDetail | GET /api/cases/{caseId} | loading, detail, not found, error, unauthorized |
| useChannelsOverview | GET /api/v1/channels/overview | loading, empty, configured, Not configured, error |
| useCustomersOverview | GET /api/v1/customers/overview | loading, empty, error |
| useSalesLeadsOverview | GET /api/v1/sales-leads/overview | loading, empty, error |
| useDocuments | Document endpoints - Requires Confirmation | loading, empty, extraction pending, review needed, error |
| useAgentRuns | AI endpoints - Requires Confirmation | loading, no outputs, answer, low confidence, blocked, error |

## State Components

| State | UX Requirement |
| --- | --- |
| Loading | Use skeletons for tables/cards and clear busy indicators for actions. |
| Empty | Explain why there is no data and what action can create/configure data. |
| Error | Show recoverable message, retry action, correlation ID where appropriate. |
| Unauthorized | Explain access issue without exposing hidden data. |
| Not configured | Tell admins which provider/configuration is missing. |
| Low confidence AI | Show confidence and require human review. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-FE-001 | All MVP screens implement loading, empty, error, unauthorized, and Not configured states. | Visual QA evidence. |
| AC-FE-002 | Role-aware navigation does not expose unauthorized actions. | RBAC UI test. |
| AC-FE-003 | Claims and document workflows can be completed without broken navigation. | Functional QA. |
| AC-FE-004 | Frontend never hardcodes fake production metrics. | Code review and environment test. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-FE-001 | Which frontend routing framework version and app directory conventions are finalized? | Affects implementation details. |
| OQ-FE-002 | What exact component library, if any, is approved? | Affects component build strategy. |
| OQ-FE-003 | Should global search be MVP functional or shell-only for first demo? | Affects scope. |
