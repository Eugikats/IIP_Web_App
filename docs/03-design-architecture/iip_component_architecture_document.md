# Component Architecture Document
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

## Chat-First Command Center Component Architecture

### Required component hierarchy

```text
CommandCenterChatPage
|-- CommandChatWorkspace
|   |-- CommandChatThread
|   |   |-- CommandMessage
|   |   |-- MetricPillStrip
|   |   |-- EmbeddedResultBlock
|   |       |-- ClaimsResultTable
|   |       |-- SlaRiskBlock
|   |       |-- DocumentGapBlock
|   |       |-- ApprovalQueueBlock
|   |       |-- DraftCommunicationBlock
|   |       |-- ManagementSummaryBlock
|   |       |-- EvidenceListBlock
|   |       |-- CommandActionBar
|   |-- CommandComposer
|-- EvidenceRail
```

### Component responsibilities

| Component | Responsibility |
|---|---|
| `CommandCenterChatPage` | Route-level data loading, authorization state, layout composition, provider-state handling. |
| `CommandChatWorkspace` | Central flex layout, scroll region, sticky composer, responsive behavior. |
| `CommandChatThread` | Ordered conversation turns and embedded structured blocks. |
| `CommandMessage` | Assistant/user message framing, timestamps, source/approval badges. |
| `MetricPillStrip` | Compact operational metrics inside an assistant message. |
| `EmbeddedResultBlock` | Standard card wrapper for claims, SLA, document, approval, and report blocks. |
| `CommandComposer` | Natural-language input, attachments/actions where configured, submit/disabled states. |
| `EvidenceRail` | Current focus, sources, confidence, selected entity, human responsibility, audit links. |

### Typed block contract

All embedded blocks must be rendered from a typed backend response. Components must not invent operational data.

```ts
interface CommandCenterResponse {
  conversationId: string;
  messageId: string;
  createdAt: string;
  tenantId: string;
  workspaceId: string;
  blocks: CommandCenterBlock[];
  evidence: EvidenceSource[];
  confidence?: "low" | "medium" | "high";
  humanReviewRequired: boolean;
  auditEventId?: string;
}
```

### Accessibility and QA rules

- Metric pills and action buttons must be keyboard-accessible.
- Each embedded table must have semantic headers.
- Confidence must be text-based as well as visual.
- The composer must have an accessible label and submit state.
- The evidence rail must be dismissible/collapsible on tablet and mobile.
- No chat response may rely on color alone for status, risk, or SLA severity.


| Field | Value |
|---|---|
| Product | Insurance Intelligence Platform (IIP) |
| Document Name | Component Architecture Document |
| Version | 1.1 - Chat-First Command Center Update |
| Status | Draft - updated for chat-first Command Center |
| Audience | Frontend engineers, UI developers, UX designers, QA leads, coding agents |
| Date | 2026-06-30 |


## 1. Purpose

This document defines reusable React/Next.js components for IIP, including cards, tables, drawers, modals, Copilot panel, document viewer, badges, timelines, forms, and navigation components.


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


## Component Architecture Principles

- Components should be business-aware but not tightly coupled to one screen unless necessary.
- Shared state components must make operational uncertainty visible.
- Components must support RBAC display rules while leaving enforcement to backend APIs.
- Components should accept structured props and avoid hardcoded demo values.
- Accessibility, keyboard navigation, density, and responsive behavior should be considered from the start.

## Core Layout Components

| Component | Responsibility | Key Props / Inputs |
| --- | --- | --- |
| AppShell | Persistent layout with navigation, top bar, main region, optional right panel | currentWorkspace, userRole, environment, alerts |
| SidebarNav | Role-aware navigation and module grouping | navItems, activeRoute, role |
| TopBar | Workspace, environment, global search, alerts, user menu | workspace, environment, user, notifications |
| RightCopilotPanel | Contextual AI assistant and evidence panel | contextType, contextId, agentRuns, permissions |
| PageHeader | Title, subtitle, actions, status chips | title, description, actions, badges |

## Data Display Components

| Component | Use Case | Implementation Notes |
| --- | --- | --- |
| MetricCard | Command Center and reports | Support trend, tooltip, source label, loading state. |
| ClaimsTable | Claims inbox | Dense table, filters, badges, row preview, keyboard friendly. |
| DocumentQueueTable | Document review | Classification, confidence, status, reviewer, action buttons. |
| CustomerSummaryCard | Customer/case context | Policy refs, contact, channel summary, risk/context flags. |
| AuditTimeline | Audit and case detail | Chronological trail with actor, action, source, timestamp. |
| ProviderHealthCard | Support & Health, Admin | Configured, Not configured, degraded, error, last checked. |

## Workflow Components

| Component | Responsibility | States |
| --- | --- | --- |
| ClaimStatusBadge | Visual status for claim lifecycle | new, incomplete, under_review, awaiting_docs, approved, rejected, closed |
| SLABadge | SLA state display | normal, at_risk, breached, paused |
| RiskSignalBadge | AI/ops risk indicator | low, medium, high, requires_review |
| ApprovalPanel | Human review gate | pending, approved, rejected, escalated |
| DocumentChecklist | Required and received documents | missing, received, low_confidence, waived, reviewed |
| ActionDrawer | Contextual actions without losing table context | open, submitting, success, error |

## AI and Evidence Components

| Component | Responsibility | Safety Requirement |
| --- | --- | --- |
| AIAnswerCard | Display AI output with citations/confidence | Must show source/evidence or low-confidence warning. |
| EvidenceList | List supporting documents, fields, conversations | No unsupported recommendation should appear as fact. |
| AgentRunStatus | Show agent execution state | queued, running, completed, blocked, failed. |
| HumanReviewBanner | Warn when human decision is required | Must appear before consequential actions. |
| PromptInjectionWarning | Display blocked unsafe input/output cases | Must be auditable. |

## Form Components

| Component | Use Case | Validation |
| --- | --- | --- |
| ClaimIntakeForm | Create or edit claim intake record | Required fields, date validation, claim type validation. |
| DocumentUploadDropzone | Upload claim documents | File type, size, duplicate check, virus scan placeholder. |
| ReviewCorrectionForm | Correct extracted fields | Reason/comment, audit event. |
| ApprovalDecisionForm | Approve/reject/escalate human decisions | Reason required, role required, audit. |
| ProviderConfigForm | Configure integration provider | Secret references only; no plain secret display. |

## Component Quality Checklist

| Check | Requirement |
| --- | --- |
| Accessibility | Labels, focus states, keyboard navigation, semantic HTML. |
| Responsiveness | Desktop-first but usable at smaller widths where needed. |
| Density | Enterprise information density without overlap or clipping. |
| State handling | No component assumes data exists. |
| Security | No hidden client-only authorization as enforcement. |
| Audit | Actions that change state pass required audit metadata. |

## Acceptance Criteria

| ID | Acceptance Criterion | Evidence |
| --- | --- | --- |
| AC-COMP-001 | Shared components cover the MVP screens without duplicated one-off patterns. | Component review. |
| AC-COMP-002 | AI output components show confidence, evidence, and review boundaries. | UX/AI safety review. |
| AC-COMP-003 | Tables, drawers, and forms handle loading, empty, error, and unauthorized states. | Visual QA. |

## Open Questions and Required Confirmations

| ID | Open Question / Confirmation Needed | Impact |
| --- | --- | --- |
| OQ-COMP-001 | Will the project standardize on a UI component library or custom Tailwind components? | Affects implementation speed. |
| OQ-COMP-002 | What exact responsive breakpoints are required for pilot? | Affects QA scope. |
| OQ-COMP-003 | Should components be documented in Storybook? | Affects documentation approach. |
