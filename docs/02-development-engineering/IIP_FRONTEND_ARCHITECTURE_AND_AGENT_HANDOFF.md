# IIP Frontend Architecture & Agent Handoff

**Product:** Insurance Intelligence Platform (IIP)  
**Purpose:** Internal insurance operations workspace for insurer staff  
**Audience:** Coding agents, frontend engineers, product designers, QA reviewers  
**Status:** Implementation handoff specification  
**Updated:** July 2026 chat-first Command Center alignment  

> **IA authority note — 2026-08-05:** The canonical `/app/*` sitemap, destination labels, permission-derived visibility, aliases, search/recent-work status, and screen/state IDs are governed by `../03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md`. Historical route examples below are non-authoritative where they differ. Protected navigation uses effective permissions returned by `/api/v1/me`; browser role preview is local/demo presentation only.

---

## 1. Command Center Direction — Read First

The **Command Center is the primary landing experience** for IIP. The default authenticated landing route must be:

```text
/app
```

Authoritative rule:

```text
The Command Center is not a dashboard with chat added to it.
The Command Center is a chat-first operational command workspace where metrics, claims, documents, evidence, approvals, actions, and reports appear as structured blocks inside the conversation.
```

Every other section in this file is subordinate to this rule. If any route, shell layout, component name, UI pattern, priority list, or acceptance criterion appears to conflict with the chat-first Command Center direction, the chat-first Command Center direction wins.

### 1.1 What chat-first means

The central experience is a **Command Chat Workspace**, not a KPI card grid. The user should land in a conversational operational workspace where the daily briefing, metric pills, claim tables, SLA risks, document gaps, approval queues, management summaries, draft communications, evidence, and actions are rendered as typed structured blocks inside the conversation thread.

The Command Center must visually prioritize:

- The central command conversation.
- Daily operational briefing as the first assistant message.
- Structured result blocks inside the thread.
- A sticky command composer.
- A right-side Context & Evidence Rail that supports the current conversation, selected record, and audit trail.

The Command Center must not visually prioritize:

- Static dashboard card grids.
- KPI tiles as the main surface.
- A small chat widget attached to a dashboard.
- Decorative AI chat that is disconnected from real claims, documents, approvals, evidence, or reports.

### 1.2 Production data and safety rules

The Command Center and all supporting frontend modules must follow these rules:

- No fake operational records in deployed environments.
- No fake metrics, mailboxes, provider states, users, claims, documents, approvals, or integration statuses.
- If a provider, integration, model, mailbox, index, workflow, or endpoint is unavailable, render **`Not configured`**, **`Unavailable`**, **`Unauthorized`**, or a real error state instead of simulated success.
- All operational answers must be tenant-scoped.
- All operational answers must be role-aware.
- All AI outputs must be source-grounded where evidence is required.
- All consequential insurance actions must remain human-reviewed.
- The AI may prepare, summarize, classify, recommend, route, and draft.
- The AI must not autonomously approve claims, reject claims, make final underwriting decisions, send high-risk customer communications, or write back to core systems without human approval.
- Browser-selected roles, workspaces, or tenant values must never be treated as authorization. Backend authorization remains mandatory.
- The frontend must not render prompts, system messages, tokens, secrets, provider exceptions, or internal-only risk/fraud notes in the chat or evidence rail.
- Untrusted content from customers, documents, email, channels, or AI responses must be safely rendered. Do not use unsafe HTML insertion.
- Every action button must call a real backend endpoint or show a proper **`Not configured`** / unavailable state.

### 1.3 Command Center first-load behavior

On first load, the Command Center should request real backend data and render it as the first assistant response:

```text
GET /api/v1/command-center/briefing
GET /api/v1/command-center/metrics
```

If these endpoints are unavailable, unauthorized, empty, degraded, or not configured, render explicit operational states. Do not fall back to local demo metrics in deployed runtime.

### 1.4 Command submission behavior

The command composer submits natural-language operational commands to the backend:

```text
POST /api/v1/command-center/query
```

The backend should return typed response blocks. The frontend should render known block types using approved block components and show a safe unsupported-block state for unknown block types.

### 1.5 Command Center response block contract

Create this shared type in the frontend types layer, preferably:

```text
src/types/command-center.ts
```

```ts
export type CommandCenterBlock =
  | { type: "text"; markdown: string }
  | { type: "metric_pills"; items: MetricPill[] }
  | { type: "claims_table"; rows: ClaimAttentionRow[]; filters?: Record<string, string> }
  | { type: "sla_summary"; items: SlaRiskItem[] }
  | { type: "document_gap_list"; items: DocumentGapItem[] }
  | { type: "approval_queue"; items: ApprovalQueueItem[] }
  | { type: "draft_communication"; draftId: string; preview: string; requiresApproval: boolean }
  | { type: "management_summary"; title: string; sections: SummarySection[] }
  | { type: "evidence_list"; sources: EvidenceSource[] }
  | { type: "action_buttons"; actions: CommandAction[] }
  | { type: "not_configured"; capability: string; message: string; adminHref?: string }
  | { type: "error"; message: string; correlationId?: string; retryable?: boolean };

export type MetricPill = {
  label: string;
  value: string | number;
  severity?: "neutral" | "good" | "warning" | "danger";
  href?: string;
};

export type ClaimAttentionRow = {
  claimId: string;
  claimNumber: string;
  customerName: string;
  status: string;
  slaStatus: "on_track" | "due_soon" | "breached";
  priority: "low" | "medium" | "high" | "critical";
  assignedTo?: string;
  lastActivityAt: string;
};

export type SlaRiskItem = {
  id: string;
  label: string;
  severity: "warning" | "danger";
  dueAt?: string;
  href?: string;
};

export type DocumentGapItem = {
  id: string;
  claimId?: string;
  documentType: string;
  reason: string;
  status: "missing" | "low_confidence" | "needs_review";
  href?: string;
};

export type ApprovalQueueItem = {
  id: string;
  title: string;
  requestedBy?: string;
  priority: "low" | "medium" | "high" | "critical";
  requiresHumanDecision: boolean;
  href?: string;
};

export type SummarySection = {
  heading: string;
  body: string;
};

export type EvidenceSource = {
  id: string;
  title: string;
  sourceType: "claim" | "policy" | "document" | "approval" | "audit" | "interaction" | "report" | "knowledge";
  href?: string;
  confidence?: number;
};

The `approval` source type is intentionally included because approval tasks are first-class evidence objects in IIP. Backend responses must not emit any other `sourceType` value unless this union and the matching backend DTO value set are updated in the same change.

export type CommandAction = {
  id: string;
  label: string;
  actionType:
    | "open_route"
    | "prepare_draft"
    | "create_approval_task"
    | "assign_work"
    | "request_documents"
    | "export_report";
  requiresConfirmation?: boolean;
  requiresHumanApproval?: boolean;
  href?: string;
};
```

---

## 2. Product Definition

IIP is an AI-native internal insurance operations platform. It is used by insurance company staff to process, review, approve, audit, and report on insurance work.

It is **not** a customer portal. Customers must never log into this application.

Customers interact through external channels such as:

- WhatsApp
- WebChat
- SMS
- Email
- Call center
- Broker/agent channels
- Website forms

Those interactions enter IIP as internal records:

- Work items
- Leads
- Claims
- Complaints
- Documents
- Service requests
- Channel conversations
- Human review tasks

IIP should feel like a premium enterprise insurance command center, not a generic dashboard, chatbot wrapper, or AI-generated prototype.

---

## 3. Product Experience Standard

The frontend must feel:

- Enterprise-grade
- Insurance-native
- AI-native
- Operational
- Calm
- Trustworthy
- Premium
- Role-aware
- Evidence-driven
- Audit-ready
- Human-approval-first

Avoid:

- Generic SaaS dashboard styling
- Legacy insurance system clutter
- Dense form-heavy screens
- Decorative AI gimmicks
- Crypto-dashboard aesthetics
- Customer portal assumptions
- Autonomous claim approval or rejection
- Uncontrolled AI decision-making

Core product principle:

```text
AI assists.
Humans decide.
Every important action is auditable.
```

---

## 4. Recommended Frontend Stack

Use the existing codebase stack if already established. If not, use:

```text
React
TypeScript
Tailwind CSS
React Router or TanStack Router
TanStack Query
Zustand or React Context for UI state
React Hook Form + Zod
Lucide React icons
Recharts
Framer Motion for subtle motion
MSAL for Microsoft Entra ID authentication
```

---

## 5. App Shell Structure

The app shell should be built around the approved chat-first Command Center layout.

Desktop layout:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Top Bar                                                                     │
├───────────────┬───────────────────────────────────────────────┬─────────────┤
│ Left Sidebar  │ Central Command Chat Workspace                │ Context &   │
│ Navigation    │ Daily briefing, metric pills, thread,         │ Evidence    │
│               │ embedded result blocks, sticky composer       │ Rail        │
└───────────────┴───────────────────────────────────────────────┴─────────────┘
```

### 5.1 Persistent left navigation

The left navigation provides stable access to the rest of the internal insurer workspace:

- Command Center
- Work Queue
- Claims
- Sales & Leads
- Channels
- Customers
- Documents
- Knowledge Base
- Reports
- Audit
- Integrations
- Admin
- AI Control Center

### 5.2 Central Command Chat Workspace

The central area is the dominant width and is the primary surface of the Command Center. It must house:

- Daily briefing as the first assistant message.
- Metric pill strip inside the conversation.
- Conversation thread.
- Structured embedded result blocks.
- Claims result tables.
- SLA risk blocks.
- Document gap blocks.
- Approval queue blocks.
- Draft communication blocks.
- Management summary blocks.
- Sticky command composer.

The central workspace should feel like an operational command interface, not a dashboard grid.

### 5.3 Context & Evidence Rail

The right rail is not a generic co-pilot panel and not a second chat interface. It exists to support the current command conversation and selected record.

It should show:

- Current focus.
- Selected claim, customer, document, work item, approval, or report.
- Evidence sources.
- Source confidence.
- Relevant audit links.
- Human-responsibility notice.
- Low-confidence warnings.
- Related records.

### 5.4 Top bar

The top bar remains sticky and should show:

- Workspace / tenant context.
- Environment badge.
- Global search.
- Alerts.
- Current user role.
- Right rail toggle.

### 5.5 Tablet/mobile behavior

- Sidebar becomes drawer.
- Context & Evidence Rail becomes slide-over panel.
- Top bar remains sticky.
- Command composer remains easy to reach.
- Structured blocks collapse into mobile-readable cards.
- Tables degrade into cards where needed.
- Final approvals should prefer tablet/desktop.

---

## 6. App Routes

Implement these routes progressively. The literal landing route is `/command-center`.

```ts
export const appRoutes = [
  { path: "/", label: "Command Center", redirectTo: "/command-center" },
  { path: "/command-center", label: "Command Center" },
  { path: "/dashboard", label: "Dashboard", legacyAliasFor: "/command-center" },
  { path: "/work-queue", label: "Work Queue" },

  { path: "/claims", label: "Claims" },
  { path: "/claims/:claimId", label: "Claim Detail" },
  { path: "/claims/:claimId/documents", label: "Claim Documents" },
  { path: "/claims-register", label: "Claims Register" },

  { path: "/sales-leads", label: "Sales & Leads" },
  { path: "/sales-leads/:leadId", label: "Lead Detail" },

  { path: "/channels", label: "Channels" },
  { path: "/channels/whatsapp", label: "WhatsApp" },
  { path: "/channels/webchat", label: "WebChat" },
  { path: "/channels/email", label: "Email Intake" },
  { path: "/channels/sms", label: "SMS" },

  { path: "/customers", label: "Customers" },
  { path: "/customers/:customerId", label: "Customer Profile" },

  { path: "/documents", label: "Documents" },
  { path: "/documents/:documentId/review", label: "Document Review" },

  { path: "/knowledge", label: "Knowledge Base" },

  { path: "/reports", label: "Reports" },
  { path: "/audit", label: "Audit & Compliance" },
  { path: "/integrations", label: "Integrations" },
  { path: "/admin", label: "Admin" },
  { path: "/ai-control-center", label: "AI Control Center" },
];
```

MVP priority:

```text
1. Command Center (/command-center) - chat-first landing route
2. Work Queue
3. Claims
4. Documents
5. Channels
6. Customers
7. Sales & Leads
8. Knowledge Base
9. Approvals / human review states where applicable
10. Reports
11. Audit
12. Integrations
13. Admin
14. AI Control Center shell
15. Dashboard legacy alias only, redirected to /command-center if referenced elsewhere
```

`/dashboard` must not be implemented as the primary experience. If older code references `/dashboard`, redirect it to `/command-center` or treat it as a legacy alias.

---

## 7. Command Center Implementation Architecture

Create the Command Center route and components using this structure:

```text
src/features/command-center/
  CommandCenterPage.tsx
  CommandChatWorkspace.tsx
  CommandChatThread.tsx
  CommandMessage.tsx
  CommandComposer.tsx
  MetricPillStrip.tsx
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

### 7.1 Command Center page responsibilities

The Command Center page must:

- Load the real daily briefing.
- Load real command metrics.
- Render loading, empty, unauthorized, error, and not configured states.
- Render the central Command Chat Workspace as the dominant surface.
- Render the Context & Evidence Rail as a supporting surface.
- Submit commands to the backend.
- Render typed response blocks.
- Never render local fake operational metrics in deployed runtime.

### 7.2 Approved command block components

Use the typed `CommandCenterBlock` contract from Section 1.5 and render blocks through `EmbeddedResultBlock`.

Component mapping:

```text
text                  -> CommandMessage markdown body
metric_pills          -> MetricPillStrip
claims_table          -> ClaimsResultTable
sla_summary           -> SlaRiskBlock
document_gap_list     -> DocumentGapBlock
approval_queue        -> ApprovalQueueBlock
draft_communication   -> DraftCommunicationBlock
management_summary    -> ManagementSummaryBlock
evidence_list         -> EvidenceRail source list or embedded evidence block
action_buttons        -> Command action row
not_configured        -> CommandNotConfiguredState
error                 -> CommandErrorState
```

Unknown block types must render a safe unsupported-block state and must not crash the Command Center.

---

## 8. Sidebar Navigation

Use a grouped sidebar, not a flat list.

Recommended sidebar:

```text
Command
  - Command Center
  - Work Queue

Workspaces
  - Claims
  - Sales & Leads
  - Policy Servicing
  - Underwriting
  - Complaints

Channels & Data
  - Channels
  - Customers
  - Documents
  - Knowledge Base

Intelligence & Governance
  - Reports
  - Audit & Compliance
  - AI Control Center

System
  - Integrations
  - Admin
```

Version 1 sidebar can be simpler:

```text
Command Center
Work Queue
Claims
Sales & Leads
Channels
Customers
Documents
Knowledge Base
Reports
Audit
Integrations
Admin
AI Control Center
```

Sidebar requirements:

- 280px expanded width
- 72px collapsed width
- Sticky full height
- Logo at top
- Workspace/tenant context
- Grouped navigation
- Active state
- Collapsed tooltip labels
- Attention badges
- Role-filtered links
- User/account section at bottom

---

## 9. Design Tokens

Add these CSS variables to the global stylesheet.

```css
:root {
  /* Brand */
  --iip-navy-950: #040458;
  --iip-navy-900: #08086b;
  --iip-blue-700: #1d4ed8;
  --iip-blue-600: #2563eb;
  --iip-cyan-500: #06b6d4;
  --iip-orange-500: #faa51a;

  /* Shell and surfaces */
  --iip-bg-shell: #f5f7fb;
  --iip-bg-page: #f8fafc;
  --iip-bg-panel: #ffffff;
  --iip-bg-card: #ffffff;
  --iip-bg-muted: #f1f5f9;
  --iip-bg-ai: #f7fbff;
  --iip-bg-ai-strong: #eef7ff;

  /* Text */
  --iip-ink: #111827;
  --iip-ink-strong: #020617;
  --iip-muted: #64748b;
  --iip-subtle: #94a3b8;
  --iip-disabled: #cbd5e1;

  /* Borders */
  --iip-border-soft: #e5e7eb;
  --iip-border-strong: #cbd5e1;
  --iip-border-ai: #bfdbfe;

  /* Status */
  --iip-success-bg: #ecfdf5;
  --iip-success-text: #047857;
  --iip-warning-bg: #fffbeb;
  --iip-warning-text: #b45309;
  --iip-danger-bg: #fef2f2;
  --iip-danger-text: #b91c1c;
  --iip-info-bg: #eff6ff;
  --iip-info-text: #1d4ed8;
  --iip-review-bg: #fff7ed;
  --iip-review-text: #c2410c;

  /* Radius */
  --iip-radius-sm: 8px;
  --iip-radius-md: 12px;
  --iip-radius-lg: 16px;
  --iip-radius-xl: 20px;

  /* Shadows */
  --iip-shadow-card: 0 1px 2px rgba(15, 23, 42, 0.06), 0 8px 24px rgba(15, 23, 42, 0.04);
  --iip-shadow-panel: 0 16px 40px rgba(15, 23, 42, 0.08);
}
```

---

## 10. Reusable UI Class Patterns

```ts
export const ui = {
  page: "space-y-6",
  pageHeader: "flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between",
  eyebrow: "text-xs font-semibold uppercase tracking-[0.16em] text-slate-500",
  h1: "text-2xl font-semibold tracking-tight text-slate-950",
  subtitle: "max-w-3xl text-sm leading-6 text-slate-600",

  card: "rounded-2xl border border-slate-200 bg-white shadow-[var(--iip-shadow-card)]",
  cardHeader: "border-b border-slate-100 px-5 py-4",
  cardBody: "px-5 py-4",

  buttonPrimary:
    "inline-flex items-center justify-center rounded-xl bg-[var(--iip-navy-950)] px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--iip-navy-900)] focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2",
  buttonSecondary:
    "inline-flex items-center justify-center rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2",

  table: "w-full border-separate border-spacing-0 text-left text-sm",
  th: "border-b border-slate-200 bg-slate-50 px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500",
  td: "border-b border-slate-100 px-4 py-3 align-middle text-slate-700",

  badge: "inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium",
};
```

---

## 11. AppShell Component

Create this file:

```text
src/components/shell/AppShell.tsx
```

The shell must treat `/command-center` as the default landing route and must render `CommandChatWorkspace` with `EvidenceRail` as the default Command Center surface. Non-Command-Center routes may still render their route content, but the Command Center must not be implemented as a generic child page with a co-pilot panel bolted on.

```tsx
import { ReactNode, useState } from "react";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import { cn } from "@/lib/utils";
import { CommandChatWorkspace } from "@/features/command-center/CommandChatWorkspace";
import { EvidenceRail } from "@/features/command-center/EvidenceRail";

type AppShellProps = {
  children?: ReactNode;
  activeRoute: string;
  userRole: string;
  workspaceName: string;
};

export function AppShell({ children, activeRoute, userRole, workspaceName }: AppShellProps) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [evidenceRailOpen, setEvidenceRailOpen] = useState(true);

  const isCommandCenter = activeRoute === "/" || activeRoute === "/command-center" || activeRoute === "/dashboard";

  return (
    <div className="min-h-screen bg-[var(--iip-bg-shell)] text-[var(--iip-ink)]">
      <div className="flex min-h-screen">
        <Sidebar
          userRole={userRole}
          collapsed={sidebarCollapsed}
          onToggle={() => setSidebarCollapsed((value) => !value)}
        />

        <div className="flex min-w-0 flex-1 flex-col">
          <TopBar
            workspaceName={workspaceName}
            evidenceRailOpen={evidenceRailOpen}
            onToggleEvidenceRail={() => setEvidenceRailOpen((value) => !value)}
          />

          {isCommandCenter ? (
            <main
              className={cn(
                "grid min-h-0 flex-1 transition-all duration-300 ease-out",
                evidenceRailOpen
                  ? "xl:grid-cols-[minmax(0,1fr)_400px]"
                  : "xl:grid-cols-[minmax(0,1fr)_0px]"
              )}
            >
              <section className="min-w-0 overflow-hidden">
                <CommandChatWorkspace />
              </section>

              <aside
                className={cn(
                  "hidden border-l border-[var(--iip-border-soft)] bg-[var(--iip-bg-panel)] transition-all duration-300 ease-out xl:block",
                  evidenceRailOpen ? "w-[400px] opacity-100" : "w-0 overflow-hidden opacity-0"
                )}
              >
                <EvidenceRail />
              </aside>
            </main>
          ) : (
            <main className="min-h-0 flex-1 overflow-y-auto px-6 py-5 lg:px-8">
              {children}
            </main>
          )}
        </div>
      </div>
    </div>
  );
}
```

### 11.1 Supporting Command Center component stubs

Create these stubs first, then connect real data and typed blocks.

```tsx
// src/features/command-center/CommandChatWorkspace.tsx
import { CommandChatThread } from "./CommandChatThread";
import { CommandComposer } from "./CommandComposer";
import { MetricPillStrip } from "./MetricPillStrip";

export function CommandChatWorkspace() {
  return (
    <div className="flex h-full min-h-0 flex-col bg-[var(--iip-bg-page)]">
      <div className="border-b border-[var(--iip-border-soft)] bg-white px-6 py-4">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">
          Command Center
        </p>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight text-slate-950">
          Ask, review, decide, and act from one governed workspace
        </h1>
        <p className="mt-1 max-w-3xl text-sm leading-6 text-slate-600">
          Metrics, claims, documents, approvals, reports, and evidence appear as structured blocks inside this conversation.
        </p>
      </div>

      <MetricPillStrip />

      <CommandChatThread />

      <CommandComposer />
    </div>
  );
}
```

```tsx
// src/features/command-center/CommandChatThread.tsx
import { CommandMessage } from "./CommandMessage";

export function CommandChatThread() {
  return (
    <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">
      <CommandMessage
        role="assistant"
        title="Daily operational briefing"
        content="Connect this component to GET /api/v1/command-center/briefing. Do not use fake deployed metrics."
      />
    </div>
  );
}
```

```tsx
// src/features/command-center/CommandMessage.tsx
import { ReactNode } from "react";

export function CommandMessage({
  role,
  title,
  content,
  children,
}: {
  role: "user" | "assistant" | "system";
  title?: string;
  content?: string;
  children?: ReactNode;
}) {
  return (
    <article className="rounded-2xl border border-slate-200 bg-white p-5 shadow-[var(--iip-shadow-card)]">
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">
        {role}
      </div>
      {title && <h2 className="mt-2 text-base font-semibold text-slate-950">{title}</h2>}
      {content && <p className="mt-2 text-sm leading-6 text-slate-600">{content}</p>}
      {children && <div className="mt-4">{children}</div>}
    </article>
  );
}
```

```tsx
// src/features/command-center/MetricPillStrip.tsx
export function MetricPillStrip() {
  return (
    <div className="border-b border-slate-200 bg-white px-6 py-3">
      <div className="flex flex-wrap gap-2">
        <div className="rounded-full border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-600">
          Metrics load from /api/v1/command-center/metrics
        </div>
      </div>
    </div>
  );
}
```

```tsx
// src/features/command-center/EmbeddedResultBlock.tsx
import type { CommandCenterBlock } from "@/types/command-center";

export function EmbeddedResultBlock({ block }: { block: CommandCenterBlock }) {
  switch (block.type) {
    case "text":
      return <div className="prose prose-sm max-w-none">{block.markdown}</div>;
    case "metric_pills":
      return <div>Render MetricPillStrip with block.items.</div>;
    case "claims_table":
      return <div>Render ClaimsResultTable with block.rows.</div>;
    case "sla_summary":
      return <div>Render SlaRiskBlock with block.items.</div>;
    case "document_gap_list":
      return <div>Render DocumentGapBlock with block.items.</div>;
    case "approval_queue":
      return <div>Render ApprovalQueueBlock with block.items.</div>;
    case "draft_communication":
      return <div>Render DraftCommunicationBlock with human approval notice.</div>;
    case "management_summary":
      return <div>Render ManagementSummaryBlock with block.sections.</div>;
    case "evidence_list":
      return <div>Render evidence sources.</div>;
    case "action_buttons":
      return <div>Render approved command actions.</div>;
    case "not_configured":
      return <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">{block.message}</div>;
    case "error":
      return <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{block.message}</div>;
    default:
      return <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">Unsupported command block.</div>;
  }
}
```

```tsx
// src/features/command-center/CommandComposer.tsx
export function CommandComposer() {
  return (
    <div className="border-t border-slate-200 bg-white px-6 py-4">
      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-2">
        <textarea
          rows={2}
          className="w-full resize-none bg-transparent px-3 py-2 text-sm outline-none placeholder:text-slate-400"
          placeholder="Ask IIP about claims, documents, SLA risks, approvals, reports, or today’s priorities..."
        />
        <div className="flex justify-end">
          <button className="rounded-xl bg-[var(--iip-navy-950)] px-4 py-2 text-sm font-semibold text-white">
            Send command
          </button>
        </div>
      </div>
      <p className="mt-2 text-[11px] leading-4 text-slate-500">
        AI may summarize, prepare, route, and draft. Humans remain responsible for consequential insurance decisions.
      </p>
    </div>
  );
}
```

```tsx
// src/features/command-center/EvidenceRail.tsx
export function EvidenceRail() {
  return (
    <div className="flex h-full flex-col bg-white">
      <div className="border-b border-slate-200 px-5 py-4">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">
          Context & Evidence
        </p>
        <h2 className="mt-1 text-base font-semibold text-slate-950">Current focus</h2>
        <p className="mt-1 text-sm leading-6 text-slate-600">
          Select a claim, document, approval, or result block to inspect evidence, confidence, and audit references.
        </p>
      </div>

      <div className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
        <section className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
          <h3 className="text-sm font-semibold text-slate-950">Selected record</h3>
          <p className="mt-2 text-sm text-slate-600">No record selected.</p>
        </section>

        <section className="rounded-2xl border border-blue-100 bg-blue-50 p-4">
          <h3 className="text-sm font-semibold text-blue-950">Sources & confidence</h3>
          <p className="mt-2 text-sm leading-6 text-blue-800">
            Evidence sources and confidence scores appear here when a grounded answer or selected block is available.
          </p>
        </section>

        <section className="rounded-2xl border border-amber-200 bg-amber-50 p-4">
          <h3 className="text-sm font-semibold text-amber-900">Human responsibility</h3>
          <p className="mt-2 text-sm leading-6 text-amber-800">
            AI output is decision support. Claims, underwriting, customer communications, and write-back actions require authorized human review where consequential.
          </p>
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white p-4">
          <h3 className="text-sm font-semibold text-slate-950">Audit references</h3>
          <p className="mt-2 text-sm text-slate-600">Related audit events will appear here.</p>
        </section>
      </div>
    </div>
  );
}
```

---

## 12. Context & Evidence Rail

The Context & Evidence Rail replaces the old generic right co-pilot panel concept for the Command Center.

It must not duplicate the chat thread. It must not act as a second chat interface. It must support the central Command Chat Workspace by showing the evidence and governance context behind the current focus.

### 12.1 Rail responsibilities

The rail shows:

- Current focus.
- Selected claim, customer, document, work item, approval, or report.
- Evidence sources.
- Confidence scores.
- Source links.
- Related records.
- Human-responsibility notice.
- Audit references.
- Low-confidence warnings.
- Not configured states for unavailable sources.

### 12.2 Rail content by context

Claims context:

- Selected claim number.
- Customer and policy context.
- Relevant documents.
- Claim summary source references.
- SLA status.
- Missing evidence.
- AI confidence.
- Human decision requirements.
- Audit events.

Documents context:

- Selected document.
- Extracted fields.
- Field confidence.
- Source page or bounding region where available.
- Validation issues.
- Human correction history.
- Audit events.

Work queue context:

- Selected work item.
- Priority and SLA rationale.
- Assignment history.
- Related claim/customer/document.
- Recommended next action.
- Human review status.

Reports context:

- Report source data.
- Filters applied.
- Generated summary evidence.
- Export audit history.

### 12.3 Rail restrictions

The rail must not:

- Render a second chat composer.
- Show raw prompts or system messages.
- Show secrets, tokens, provider exceptions, or internal configuration details.
- Present AI output as a final decision.
- Replace the central command conversation.

---

## 13. Work Queue Model

Everything entering the system should become a work item.

Examples:

- WhatsApp lead
- Claim FNOL
- Document review
- Complaint
- Policy service request
- Low-confidence extraction
- Human approval task
- SLA risk
- Integration failure

```ts
export type WorkItemType =
  | "claim"
  | "lead"
  | "document_review"
  | "complaint"
  | "service_request"
  | "underwriting"
  | "approval"
  | "integration_exception";

export type WorkItemStatus =
  | "new"
  | "assigned"
  | "in_review"
  | "waiting_customer"
  | "waiting_officer"
  | "approved"
  | "closed"
  | "escalated";

export type SLAStatus = "on_track" | "due_soon" | "breached";

export type WorkItem = {
  id: string;
  reference: string;
  type: WorkItemType;
  title: string;
  customerName?: string;
  channel?: "whatsapp" | "webchat" | "sms" | "email" | "broker" | "manual";
  priority: "low" | "medium" | "high" | "critical";
  slaStatus: SLAStatus;
  assignedTo?: string;
  aiConfidence?: number;
  status: WorkItemStatus;
  lastActivityAt: string;
};
```

Work queue columns:

```text
Reference
Type
Customer
Channel
Priority
SLA
AI Confidence
Assigned To
Status
Last Activity
Actions
```

Required filters:

```text
My work
Unassigned
Claims
Leads
Complaints
Document review
Low confidence
SLA risk
Awaiting customer
Awaiting officer
Escalated
```

---

### 13.1 Work Queue wire contract

Endpoint returning this shape:

```text
GET /api/v1/work-queue
```

The backend response item shape must match the `WorkItem` TypeScript type above. JSON fields must remain camelCase. Backend-only tenant, workspace, permission, assignment-history, and audit metadata must remain excluded from this wire DTO unless the frontend type is intentionally extended in the same contract change.

Shared allowed value sets:

```ts
export const workItemTypeValues = [
  "claim",
  "lead",
  "document_review",
  "complaint",
  "service_request",
  "underwriting",
  "approval",
  "integration_exception",
] as const;

export const workItemStatusValues = [
  "new",
  "assigned",
  "in_review",
  "waiting_customer",
  "waiting_officer",
  "approved",
  "closed",
  "escalated",
] as const;

export const slaStatusValues = ["on_track", "due_soon", "breached"] as const;
export const workItemPriorityValues = ["low", "medium", "high", "critical"] as const;
export const workItemChannelValues = ["whatsapp", "webchat", "sms", "email", "broker", "manual"] as const;
```

## 14. Claims Workspace

Claims should be the strongest Version 1 workspace.

Routes:

```text
/claims
/claims/:claimId
/claims/:claimId/documents
/claims-register
```

Claim detail layout:

```text
Top:
Claim number, policy number, status, priority, SLA, assigned officer

Left:
Claim summary
Customer details
Policy context
FNOL details

Center:
Timeline
Documents
Customer communication history
Internal notes
Decision panel

Right:
AI co-pilot / evidence support area where route context requires it
AI claim brief
Missing documents
Next actions
Evidence/citations
Confidence

Bottom:
Audit trail
Human approval log
Write-back history
```

AI claim brief must include:

- Short summary
- Date and location of loss
- Policy status
- Coverage context
- Documents received
- Documents missing
- Low-confidence extractions
- SLA risk
- Recommended next human action
- Sources used

Never build autonomous final approval or rejection.

Allowed:

- AI-suggested next action
- AI-drafted customer update
- AI-drafted internal memo
- AI-drafted missing document request
- AI-drafted assessor instruction
- Human decision panel
- Human approval before write-back

Not allowed:

- AI approves claim
- AI rejects claim
- AI makes final underwriting decision
- AI sends high-risk communication without review

---

### 14.1 Claims frontend contract

Endpoint returning this shape:

```text
GET /api/v1/claims/{claimId}
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/claim.ts
```

```ts
export type ClaimDetail = {
  claimId: string;
  claimNumber: string;
  policyNumber: string;
  status: string; // TODO: confirm canonical claim status value set.
  priority: "low" | "medium" | "high" | "critical";
  slaStatus: "on_track" | "due_soon" | "breached";
  assignedOfficer?: string;

  claimSummary?: string;
  customerDetails?: Record<string, string>; // TODO: confirm structured CustomerProfile reuse.
  policyContext?: Record<string, string>; // TODO: confirm policy summary fields.
  fnolDetails?: Record<string, string>; // TODO: confirm FNOL field set.

  timeline: ClaimTimelineItem[];
  documents: ClaimDocumentSummary[];
  customerCommunicationHistory: ClaimCommunicationSummary[];
  internalNotes: ClaimInternalNote[];

  aiClaimBrief?: ClaimAiBrief;
  auditTrail: AuditEventRow[];
  humanApprovalLog: ApprovalQueueRow[];
  writeBackHistory: ClaimWriteBackHistoryItem[];
};

export type ClaimTimelineItem = {
  id: string;
  title: string;
  occurredAt: string;
  description?: string;
};

export type ClaimDocumentSummary = {
  documentId: string;
  documentType: string;
  status: string; // TODO: confirm document status value set for claim detail.
  confidence?: number;
  href?: string;
};

export type ClaimCommunicationSummary = {
  id: string;
  channel: "whatsapp" | "webchat" | "sms" | "email" | "call_center" | "broker" | "website_form";
  summary: string;
  lastActivityAt: string;
};

export type ClaimInternalNote = {
  id: string;
  authorName?: string;
  body: string;
  createdAt: string;
};

export type ClaimAiBrief = {
  shortSummary: string;
  dateOfLoss?: string;
  locationOfLoss?: string;
  policyStatus?: string;
  coverageContext?: string;
  documentsReceived: string[];
  documentsMissing: string[];
  lowConfidenceExtractions: string[];
  slaRisk?: string;
  recommendedNextHumanAction?: string;
  sourcesUsed: EvidenceSource[];
};

export type ClaimWriteBackHistoryItem = {
  id: string;
  targetSystem: string;
  status: string; // TODO: confirm write-back status value set.
  occurredAt: string;
  summary?: string;
};
```

Backend-only fields such as tenant ID, workspace ID, raw provider payloads, internal risk flags, and hidden decision metadata must stay excluded from this wire DTO unless the frontend type is explicitly extended in the same change.



## 14A. Approvals Workspace Contract

Approvals are human-review tasks used when AI or workflow automation prepares a consequential action. They support human ownership of claims decisions, customer communications, exports, and write-back requests.

Endpoint returning this shape:

```text
GET /api/v1/approvals/queue
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/approval.ts
```

```ts
export type ApprovalQueueRow = {
  id: string;
  title: string;
  requestedBy?: string;
  priority: "low" | "medium" | "high" | "critical";
  requiresHumanDecision: boolean;
  href?: string;
};
```

This intentionally mirrors the Command Center `ApprovalQueueItem` shape so approvals can appear both in the dedicated Approvals workspace and as structured Command Center blocks. Backend-only approval workflow state, approver IDs, tenant/workspace IDs, and full audit metadata must remain excluded unless the frontend type is intentionally extended in the same contract change.

## 15. Documents Workspace

Routes:

```text
/documents
/documents/:documentId/review
/documents/missing
/documents/extraction-results
```

Document review layout:

```text
Left:
Document viewer

Right:
Extracted fields
Confidence scores
Required corrections
Validation issues

Bottom:
Source mapping
Audit trail
Reviewer actions
```

```ts
export type ExtractedField = {
  id: string;
  fieldName: string;
  label: string;
  value: string;
  confidence: number;
  sourcePage?: number;
  sourceBoundingBox?: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  status: "accepted" | "needs_review" | "corrected" | "rejected";
};
```

---

### 15.1 Documents wire contract

Endpoint returning this shape:

```text
GET /api/v1/documents/{documentId}/review
```

The document review response must include extracted fields that match the `ExtractedField` TypeScript type above. JSON fields must remain camelCase. Backend-only raw OCR payloads, provider metadata, blob paths, tenant/workspace identifiers, and raw document-intelligence response bodies must remain excluded from the wire DTO unless the frontend type is intentionally extended in the same contract change.

Shared allowed value set:

```ts
export const extractedFieldStatusValues = [
  "accepted",
  "needs_review",
  "corrected",
  "rejected",
] as const;
```

## 16. Channels Workspace

Channels are where external customer interactions enter the system.

Routes:

```text
/channels
/channels/whatsapp
/channels/webchat
/channels/email
/channels/sms
/channels/templates
```

Channel inbox columns:

```text
Customer
Channel
Detected intent
Suggested routing
AI confidence
Status
Assigned to
Last message
Last activity
```

Detected intents:

```text
Claim/FNOL
Quote request
Renewal
Complaint
Policy inquiry
Document upload
Payment inquiry
General service
```

Channels should create internal work items. Do not create customer-facing login screens.

---

### 16.1 Channels frontend contract

Endpoint returning this shape:

```text
GET /api/v1/channels/overview
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/channel.ts
```

```ts
export type ChannelConversationRow = {
  id: string;
  customerName: string;
  channel: "whatsapp" | "webchat" | "sms" | "email" | "call_center" | "broker" | "website_form";
  detectedIntent:
    | "claim_fnol"
    | "quote_request"
    | "renewal"
    | "complaint"
    | "policy_inquiry"
    | "document_upload"
    | "payment_inquiry"
    | "general_service";
  suggestedRouting: string;
  aiConfidence: number;
  status: string; // TODO: confirm channel conversation status value set.
  assignedTo?: string;
  lastMessage: string;
  lastActivityAt: string;
};
```

Backend-only message bodies, raw attachments, hidden risk notes, tenant/workspace IDs, and provider payload metadata must stay excluded from this list-row DTO unless the frontend type is intentionally extended in the same contract change.

## 17. Sales & Leads Workspace

Routes:

```text
/sales-leads
/sales-leads/:leadId
/sales-leads/quote-requests
/sales-leads/follow-ups
```

Lead detail sections:

- Lead source
- Customer need
- Conversation summary
- Product fit
- Quote status
- Required information
- Follow-up actions
- Assigned sales officer
- Conversion probability placeholder

---

### 17.1 Sales & Leads frontend contract

Endpoint returning this shape:

```text
GET /api/v1/sales-leads/{leadId}
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/sales-lead.ts
```

```ts
export type SalesLeadDetail = {
  leadId: string;
  leadSource: string;
  customerNeed: string;
  conversationSummary: string;
  productFit: string;
  quoteStatus: string; // TODO: confirm quote status value set.
  requiredInformation: string[];
  followUpActions: string[];
  assignedSalesOfficer?: string;
  conversionProbability?: number;
};
```

Backend-only scoring inputs, hidden model notes, tenant/workspace IDs, raw channel transcripts, and provider metadata must stay excluded unless the frontend type is intentionally extended in the same contract change.

## 18. Customers Workspace

Customers do not log in, but insurer staff need an internal Customer 360.

Customer profile sections:

- Identity
- Policies
- Claims
- Interactions
- Documents
- Consent
- Payments
- Opportunities
- Audit history

Do not include:

- Customer account settings
- Customer password
- Customer dashboard preferences
- Customer app onboarding

---

### 18.1 Customers frontend contract

Endpoint returning this shape:

```text
GET /api/v1/customers/{customerId}
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/customer.ts
```

```ts
export type CustomerProfile = {
  customerId: string;
  identity: Record<string, string>; // TODO: confirm canonical customer identity fields.
  policies: Record<string, string>[]; // TODO: confirm policy summary row fields.
  claims: Record<string, string>[]; // TODO: confirm claim summary row fields.
  interactions: Record<string, string>[]; // TODO: confirm interaction summary row fields.
  documents: Record<string, string>[]; // TODO: confirm document summary row fields.
  consent: Record<string, string>; // TODO: confirm consent summary fields.
  payments: Record<string, string>[]; // TODO: confirm payment summary row fields.
  opportunities: Record<string, string>[]; // TODO: confirm opportunity summary row fields.
  auditHistory: AuditEventRow[];
};
```

The current frontend contract deliberately keeps several Customer 360 sections as `Record<string, string>` because the existing product prose defines section names but not final field-level business schemas. Backend-only IDs, raw policy-system payloads, hidden risk notes, tenant/workspace IDs, and sensitive internal metadata must stay excluded until the detailed Customer 360 contract is confirmed.

## 19. Reports, Audit, Integrations, Admin

### Reports

Screens:

```text
Executive Dashboard
Claims Reports
Sales Reports
Channel Reports
SLA Reports
Compliance Reports
AI Performance Reports
Export Center
```

### Audit

Screens:

```text
Audit Log
Consent Records
Human Approval Logs
SLA Compliance
Data Access Logs
Regulatory Exports
Workflow Versions
```

Audit must track:

- User login
- Data viewed
- Data changed
- Document uploaded
- AI extraction generated
- Human correction made
- Claim decision made
- Customer communication sent
- Consent captured
- Workflow changed
- Report exported

### Audit frontend contract

Endpoint returning this shape:

```text
GET /api/v1/audit/events
```

Create this shared type in the frontend types layer, preferably:

```text
src/types/audit.ts
```

```ts
export type AuditEventType =
  | "user_login"
  | "data_viewed"
  | "data_changed"
  | "document_uploaded"
  | "ai_extraction_generated"
  | "human_correction_made"
  | "claim_decision_made"
  | "customer_communication_sent"
  | "consent_captured"
  | "workflow_changed"
  | "report_exported";

export type AuditEventRow = {
  id: string;
  eventType: AuditEventType;
  actorUserId?: string;
  targetType?: string; // TODO: confirm target type value set.
  targetId?: string;
  summary: string;
  occurredAt: string;
  correlationId?: string;
};
```

Backend-only sensitive details, raw before/after payloads, hidden prompts, system messages, provider exceptions, and secrets must stay excluded from the audit list-row DTO. Detailed audit-event inspection may use a separate endpoint and contract after approval.

### Integrations

Screens:

```text
Integration Overview
WhatsApp
Email/Outlook
Policy System
Claims System
CRM
Mobile Money
Document Storage
API Keys
Webhooks
Integration Logs
```

### Admin

Screens:

```text
Organization Settings
Users
Roles & Permissions
Departments
Products
Workflows
SLA Rules
Document Rules
Templates
AI Settings
Tenant Settings
```

---

## 20. AI Control Center

Screens:

```text
Agent Library
Agent Performance
Prompt / Instruction Settings
Confidence Thresholds
Human Review Rules
Model Logs
Knowledge Coverage
AI Exceptions
```

```ts
export type AgentDefinition = {
  id: string;
  name: string;
  purpose: string;
  status: "active" | "draft" | "paused" | "disabled";
  confidenceThreshold: number;
  humanReviewRequired: boolean;
  lastRunAt?: string;
  ownerTeam: "claims" | "sales" | "service" | "compliance" | "admin";
};
```

Agents to display:

- Supervisor / Router Agent
- Claims Intake Agent
- Document Intelligence Agent
- Policy Intelligence Agent
- Compliance Agent
- Lead Qualification Agent
- Complaint Agent
- Renewal Agent
- Underwriting Support Agent

---

## 20A. Frontend/Backend Module Contract Consistency Rule

For every module-level wire contract, the backend DTO is the source of truth for the serialized response shape. The frontend TypeScript type must be generated from, or manually kept in lockstep with, the matching backend DTO. Any future field addition, field removal, enum value change, type rename, or discriminator change must update both this frontend handoff document and `iip_backend_code_implementation_specification.md` in the same change.

The enforcement mechanism is the backend module contract consistency test suite listed in the backend specification.

### 20A.1 Module contract reconciliation summary

| Module | Frontend type name | Backend DTO name | Status |
|---|---|---|---|
| Work Queue | `WorkItem` | `WorkItemDto` | matched |
| Documents | `ExtractedField` | `ExtractedFieldDto` | matched |
| Claims | `ClaimDetail` | `ClaimDetailDto` | newly created; several subsection fields need further confirmation |
| Approvals | `ApprovalQueueRow` | `ApprovalQueueRowDto` | newly created |
| Channels | `ChannelConversationRow` | `ChannelConversationRowDto` | newly created; status value set needs further confirmation |
| Customers | `CustomerProfile` | `CustomerProfileDto` | newly created; detailed Customer 360 field schemas need further confirmation |
| Sales & Leads | `SalesLeadDetail` | `SalesLeadDetailDto` | newly created; quote status value set needs further confirmation |
| Audit | `AuditEventRow` | `AuditEventRowDto` | newly created; target type value set needs further confirmation |

## 21. Required Frontend States

Every route must handle:

- Loading
- Skeleton loading
- Empty state
- Error state
- Unauthorized
- Not configured
- No integration connected
- Low confidence / needs review
- Offline / connection interrupted
- Permission denied

---

## 22. Motion Rules

Use motion sparingly and only where useful.

Recommended timings:

```text
Fast hover: 120ms
Button press: 80ms
Drawer open/close: 220ms
Page section fade: 180ms
Skeleton shimmer: 1200ms
AI streaming: token-by-token or paragraph-by-paragraph
Toast entrance: 180ms
Toast exit: 120ms
```

Add motion to:

- Sidebar collapse
- Context & Evidence Rail open/close
- Drawer transitions
- Table row preview
- Document upload progress
- AI response streaming
- Status change confirmation
- Approval completion
- Error recovery

Do not add distracting motion to:

- Critical decision buttons
- Dense data tables
- Audit log entries
- Compliance evidence

---

## 23. Recommended File Structure

```text
src/
  app/
    App.tsx
    routes.tsx
    providers.tsx

  components/
    shell/
      AppShell.tsx
      Sidebar.tsx
      TopBar.tsx
      MobileSidebar.tsx

    ui/
      Button.tsx
      Card.tsx
      Badge.tsx
      Tabs.tsx
      Table.tsx
      Drawer.tsx
      Modal.tsx
      EmptyState.tsx
      LoadingState.tsx
      ErrorState.tsx
      NotConfiguredState.tsx

    work/
      WorkQueueTable.tsx
      WorkItemPriorityBadge.tsx
      SlaStatusBadge.tsx

    claims/
      ClaimHeader.tsx
      ClaimSummaryPanel.tsx
      ClaimTimeline.tsx
      ClaimDocuments.tsx
      ClaimDecisionPanel.tsx
      ClaimAuditTrail.tsx

    documents/
      DocumentViewer.tsx
      ExtractionFieldsPanel.tsx
      ConfidenceMeter.tsx

    ai/
      AiBriefCard.tsx
      AiSourcesList.tsx
      AiConfidencePanel.tsx
      HumanReviewNotice.tsx

  config/
    sidebar.ts
    routes.ts
    roles.ts

  features/
    command-center/
      CommandCenterPage.tsx
      CommandChatWorkspace.tsx
      CommandChatThread.tsx
      CommandMessage.tsx
      CommandComposer.tsx
      MetricPillStrip.tsx
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
    work-queue/
      WorkQueuePage.tsx
    claims/
      ClaimsPage.tsx
      ClaimDetailPage.tsx
    sales-leads/
      SalesLeadsPage.tsx
      LeadDetailPage.tsx
    channels/
      ChannelsPage.tsx
    customers/
      CustomersPage.tsx
      CustomerProfilePage.tsx
    documents/
      DocumentsPage.tsx
      DocumentReviewPage.tsx
    knowledge/
      KnowledgeBasePage.tsx
    reports/
      ReportsPage.tsx
    audit/
      AuditPage.tsx
    integrations/
      IntegrationsPage.tsx
    admin/
      AdminPage.tsx
    ai-control-center/
      AiControlCenterPage.tsx

  lib/
    utils.ts
    auth.ts
    api.ts

  types/
    command-center.ts
    work-item.ts
    claim.ts
    customer.ts
    document.ts
    ai.ts
    audit.ts

  styles/
    globals.css
```

---

## 24. Implementation Priority

Build in this order:

```text
1. Chat-first Command Center shell: central command thread, sticky composer, metric pill strip, typed response block renderer, and Context & Evidence Rail
2. Command Center backend hooks for briefing, metrics, and query response states
3. No-fake-data state handling: loading, empty, error, unauthorized, Not configured, degraded
4. Sidebar navigation
5. Top bar with workspace, search, alerts, role, and evidence rail toggle
6. Design tokens
7. Work Queue
8. Claims list
9. Claim detail shell
10. Document review shell
11. Shared UI states across all modules
12. Channels, Customers, Sales & Leads overview pages
13. Reports/Audit/Integrations/Admin placeholder pages
14. AI Control Center shell
15. Legacy /dashboard redirect to /command-center if needed
```

Use typed mock data only in isolated local development fixtures or tests. Do not ship fake operational records or fake metrics in deployed runtime.

---

## 25. Acceptance Criteria

The frontend is acceptable only if:

- The app clearly looks like an internal insurance operations platform.
- Customers cannot log into the app.
- Customer interactions appear only as channel records, work items, leads, claims, complaints, documents, or service requests.
- `/command-center` is the primary authenticated landing route.
- `/dashboard` is only a legacy alias or redirect if referenced elsewhere.
- The Command Center must visually prioritize the central command-chat workspace over static dashboard cards.
- No screen may present the Command Center as a KPI-grid dashboard with chat as a secondary feature.
- Command Center metrics, claims, documents, evidence, approvals, actions, and reports render as structured blocks inside the conversation.
- Command Center action buttons call real backend endpoints or show a proper unavailable / Not configured state.
- The Context & Evidence Rail supports the central command conversation and does not duplicate it as a second chat thread.
- The left sidebar is role-aware.
- The top bar shows workspace, search, alerts, role, and evidence rail toggle.
- The Work Queue remains a major operational module.
- Claims workspace supports AI brief, documents, timeline, SLA, decision panel, and audit trail.
- Document review supports confidence, extraction review, and human correction.
- Audit and human approval are visible, not hidden.
- No AI flow performs autonomous claim approval or rejection.
- All high-risk AI outputs require human review.
- Design tokens are centralized.
- Every page has loading, empty, error, unauthorized, and not configured states.
- The UI feels premium, calm, and enterprise-grade.

---

## 26. Non-Negotiable Quality Bars

1. IIP must feel like a serious enterprise insurance product.
2. The Command Center must be chat-first, not dashboard-first.
3. The Command Center must visually prioritize the central command-chat workspace over static dashboard cards.
4. No screen may present the Command Center as a KPI-grid dashboard with chat as a secondary feature.
5. Metrics, claims, documents, evidence, approvals, actions, and reports must appear as structured blocks inside the Command Center conversation.
6. The Work Queue must remain a major operational module.
7. The Context & Evidence Rail must show current focus, sources, confidence, selected record, human responsibility, and audit references.
8. Claims must be evidence-driven and audit-ready.
9. Documents must show confidence and human correction states.
10. Customer interactions must enter through channels, not customer login.
11. Human review must be required for high-risk decisions.
12. Audit trails must be visible and respected in the UI.
13. The design must be calm, consistent, and premium.
14. The frontend must be structurally ready for real APIs later.

---

## 27. Final Product Standard

IIP should feel like:

```text
A premium AI-native insurance operations workspace for insurer staff,
where the Command Center is a chat-first operational command workspace,
where metrics, claims, documents, evidence, approvals, actions, and reports appear inside the conversation,
where channel interactions become governed work items,
AI assists but does not replace human judgment,
documents and claims are evidence-linked,
integrations feed clean canonical data,
and every important action is auditable.
```
