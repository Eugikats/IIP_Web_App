# Insurance Intelligence Platform (IIP) - UI/UX Handoff Document

**Document type:** UI/UX Design and Product Behavior Handoff  
**Product name:** Insurance Intelligence Platform  
**Acronym:** IIP  
**Prepared for:** Otic Group / IIP Product, Design, Engineering, AI, Cloud, QA, and Implementation Agents  
**Date:** 30 June 2026  
**Version:** 1.1 - Chat-First Command Center Update - Consolidated UI/UX Handoff  
**Status:** Working implementation baseline - updated for chat-first Command Center  
**Classification:** Internal - Product, Design, Engineering, AI, QA, Cloud, and Delivery Reference

---

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

## Chat-First Command Center Amendment

### Command Center design decision

The Command Center must be implemented as the main conversational operations surface of IIP Operations. It should use the familiar interaction pattern of modern AI assistants, but adapted for regulated insurance operations.

The center of the page must be the largest and most important workspace. It should contain the primary chat thread, the daily operational briefing, structured metric pills, suggested prompts, embedded result cards, human-review actions, and a sticky composer.

The Command Center must not be implemented as a static KPI dashboard with a small chat widget. The dashboard information should live inside the conversation as structured, clickable, source-grounded operational blocks.

### Required page structure

```text
IIP Operations shell
|-- Left navigation
|-- Top bar
|-- Main Command Chat Workspace
|   |-- Daily operational briefing
|   |-- Metric pill strip
|   |-- Suggested prompt chips
|   |-- Conversation stream
|   |-- Embedded operational blocks
|   |-- Sticky command composer
|-- Right Context & Evidence Rail
```

### Main Command Chat Workspace

The central workspace must support these first-load elements:

1. greeting and daily operational briefing;
2. metric pills for open claims, SLA breaches, awaiting approval, incomplete claims, customer update backlog, document completeness, and AI quality;
3. suggested prompts for urgent claims, SLA risk, missing documents, customer updates, approvals, and management reports;
4. conversation thread with structured AI responses;
5. embedded claims/SLA/document/approval/result blocks;
6. clear human responsibility messaging;
7. sticky composer for natural-language commands.

### Metric pills inside conversation

Metrics should be rendered as compact, clickable pills inside the briefing or agent response rather than as a dominant dashboard grid. Clicking a metric pill should either:

- submit a related command to the chat;
- reveal an embedded filtered result block;
- navigate to the relevant operational module with filters applied; or
- open the right rail with related evidence and source context.

### Embedded response blocks

The chat thread must support structured response blocks, including:

| Block type | Purpose |
|---|---|
| `metric_pills` | Compact operational snapshot within the conversation. |
| `claims_table` | Claims needing attention, filtered by status, SLA, risk, product line, assignment, or channel. |
| `sla_summary` | SLA breach/at-risk breakdown with next actions. |
| `document_gap_list` | Missing or incomplete document requirements. |
| `approval_queue` | Human-review items and proposed next steps. |
| `draft_communication` | Customer-safe draft messages that require approval where needed. |
| `management_summary` | Executive-ready daily/weekly summary generated from real data. |
| `evidence_list` | Sources, confidence, timestamps, and audit references. |
| `action_buttons` | Review, open, request documents, prepare draft, export, assign, or escalate. |

### Right Context & Evidence Rail

The right panel should not duplicate the main chat. It should act as an evidence and context rail showing:

- current focus;
- sources used;
- confidence level;
- selected claim/customer/document details;
- human responsibility warning;
- provider/data freshness;
- audit references;
- related next actions.

### Mobile and responsive behavior

On smaller screens, the chat remains the primary surface. The right evidence rail should collapse into a drawer, sheet, or tab. The sticky composer must remain accessible without hiding critical content. Final approvals, settlement actions, disputed claim decisions, and administrative configuration remain desktop/tablet-preferred.

## Document Control

| Field | Value |
|---|---|
| Owner | Product / Design / Delivery Lead |
| Primary audience | Frontend engineers, backend engineers, AI engineers, designers, QA, implementation agents, project leadership |
| Source baseline | Master Product Context, PRD, FSD, Technical Architecture Document, System Integration Strategy, chat UI/UX notes, Antigravity/codebase findings, meeting notes, product strategy findings |
| Official name | Insurance Intelligence Platform (IIP) |
| Superseded name | ACIP / Agentic Insurance Intelligence Platform |
| MVP focus | Motor Claims and Document Intelligence for an insurance company pilot |
| UI direction | Chat-first, three-panel enterprise operations workspace |
| Decision model | AI assists. Humans make regulated insurance decisions. |
| Customer app rule | Customers do not log into the internal staff app in V1. External customer interactions feed internal workspaces through WhatsApp, email, web, broker, and call-center channels. |

---

## 1. Executive Summary

The **Insurance Intelligence Platform (IIP)** user experience must feel like an intelligent insurance operations command center, not like a traditional insurance dashboard with a chatbot added onto it.

The interface should help insurer staff understand what needs attention, what the AI has prepared, what evidence supports the recommendation, what action is blocked, what requires human review, and what must happen next. The platform must make intelligence visible in every workflow, while keeping regulated insurance decisions under human control.

The target design pattern is a **chat-first, three-panel enterprise workspace**:

```text
Persistent left navigation
+
Central work/chat workspace
+
Right contextual Copilot panel
```

The central workspace should support both conversational interaction and structured operational UI. The right Copilot panel should update according to the selected claim, customer, lead, document, channel conversation, workflow, or report. The left navigation should remain role-aware and provide access to the major operational workspaces.

This document gives implementation-ready guidance for:

- Information architecture
- Global layout
- Navigation
- Screen behavior
- Role-aware experiences
- Module-specific UX
- AI/Copilot interaction rules
- Human review and approval patterns
- Loading, empty, error, unauthorized, and not-configured states
- Component requirements
- Responsive behavior
- Accessibility requirements
- API assumptions
- QA acceptance criteria
- Implementation instructions for AI coding agents

---

## 2. Product Experience Principles

### 2.1 Core UX Principle

> **IIP is an intelligent insurance workflow engine, not just an insurance portal.**

The AI should not live only in a chatbot. Intelligence must appear across the product through:

- AI summaries
- Evidence cards
- Missing-document checklists
- Risk and SLA signals
- Suggested next actions
- Source citations
- Confidence indicators
- Human approval gates
- Audit trails
- Smart routing
- Workflow status explanations
- Customer-safe communication drafts

### 2.2 Human Decision Principle

> **AI prepares insurance work. Humans make regulated insurance decisions.**

The UI must never imply that AI is making final claims, underwriting, fraud, payment, cancellation, pricing, or complaint decisions.

Use language such as:

- AI suggestion
- AI-prepared summary
- Suggested next action
- Advisory fraud signal
- Requires human review
- Pending claims officer decision
- Pending supervisor approval
- Evidence used
- Confidence level

Avoid language such as:

- AI approved
- AI rejected
- AI decided
- Fraud confirmed by AI
- Settlement authorized by AI
- Policy bound by AI
- Automatic denial
- Autonomous underwriting

### 2.3 Internal Workspace Principle

The V1 app is an **internal insurer workspace**.

Customers should not log into the internal app in V1. Instead:

- Customers interact through WhatsApp, web forms, email, SMS, broker, call center, or future customer-safe portal.
- Those interactions create internal channel conversations, leads, claims, tasks, documents, and customer records.
- Internal staff use IIP to review, validate, route, approve, communicate, and audit.

### 2.4 Evidence-First Principle

Every AI output that affects a claim, policy, customer, document, underwriting workflow, complaint, payment, or customer communication must show evidence or uncertainty.

The UI must show:

- Source document or record
- Extracted field confidence
- Evidence ID or document reference
- Whether the output is grounded or uncertain
- Required human action
- Audit trail status

### 2.5 Role-Aware Principle

The platform must be role-aware.

A claims officer, supervisor, underwriter, customer service agent, compliance officer, insurer admin, executive, and Otic platform admin should not see the same interface, actions, or permissions.

Role-awareness must be enforced server-side. The frontend may adapt the UI, but frontend role checks are never a security boundary.

---

## 3. Target Users and Experience Goals

| User group | Primary need | UX goal |
|---|---|---|
| Claims Officer | Process claims quickly and accurately | Show claim context, documents, AI summary, missing items, risk flags, and next steps in one workspace |
| Claims Supervisor | Manage escalations, workload, SLA risk, and approvals | Show queue health, at-risk claims, approvals, assignment load, and evidence before approval |
| Customer Service Agent | Answer customer questions and draft safe updates | Show customer status, approved knowledge, communication history, and safe response drafts |
| Underwriter | Review submissions and missing risk information | Show extracted submission data, risk summary, missing fields, and referral conditions |
| Compliance Officer | Review audit trails and controlled decision records | Show AI actions, human actions, evidence used, overrides, exports, and decision lineage |
| Insurer Admin | Configure roles, workflows, document types, channels, and integrations | Show configuration status, gaps, errors, and safe defaults |
| Executive / Operations Manager | Track performance, risk, ROI, and operational bottlenecks | Show KPI cards, trend insights, SLA status, productivity, and financial impact |
| Otic Platform Admin | Monitor platform health, tenant support, and incidents | Show provider health, queues, deployment state, failed integrations, support access, and logs |

---

## 4. Information Architecture

### 4.1 Primary Navigation

The left navigation should organize the system around insurance work, not around technical modules.

```text
Command Center
Channels
Sales & Leads
Customers
Claims
Documents
Knowledge Base
Approvals
Compliance & Audit
Reports & Analytics
Integrations
Admin Settings
Support & Health
```

### 4.2 Navigation Groups

| Group | Items | Purpose |
|---|---|---|
| Command | Command Center | Daily operational entry point |
| Acquisition | Channels, Sales & Leads, Customers | Lead capture, customer interaction, Customer 360 |
| Claims Operations | Claims, Documents, Approvals | Claims work queue, FNOL, case review, document intelligence, human review |
| Intelligence | Knowledge Base, Reports & Analytics | Approved knowledge, operational insight, AI quality, ROI |
| Control | Compliance & Audit, Integrations, Admin Settings, Support & Health | Governance, configuration, system health, enterprise controls |

### 4.3 V1 Screen Inventory

| Priority | Screen | V1 status |
|---:|---|---|
| 1 | Login / tenant selection | Required |
| 2 | Command Center | Required |
| 3 | Channels Inbox | Required |
| 4 | Sales & Leads Inbox | Required |
| 5 | Customer 360 | Required |
| 6 | Claims Inbox / Work Queue | Required |
| 7 | FNOL / New Claim Intake | Required |
| 8 | Case Review | Required |
| 9 | Document Extraction Review | Required |
| 10 | AI Assistant / Contextual Copilot | Required |
| 11 | Knowledge Base | Required |
| 12 | Approvals | Required |
| 13 | Compliance & Audit | Required |
| 14 | Reports & Analytics | Required basic version |
| 15 | Integrations | Required basic version |
| 16 | Admin Settings | Required |
| 17 | Support & Health | Required |
| 18 | Client Portal | Later phase / customer-safe extension |

---

## 5. Global Layout Framework

### 5.1 Three-Panel Architecture

The standard authenticated app shell should use:

```text
+--------------------------------------------------------------------------------+
| Top Bar                                                                         |
| Workspace | Environment | Search | Alerts | Role | User profile              |
+-------------------+---------------------------------------+--------------------+
| Left Navigation   | Main Workspace                        | Right Copilot      |
| Role-aware        | Chat + structured operational UI      | Context-aware      |
| Collapsible       | Tables, cards, forms, timelines       | Evidence/actions   |
+-------------------+---------------------------------------+--------------------+
| Status Bar: connection | last sync | active workflows | queue status | health |
+--------------------------------------------------------------------------------+
```

### 5.2 Layout Dimensions

| Component | Desktop target | Behavior |
|---|---:|---|
| Top bar height | 64 px | Persistent |
| Left navigation expanded | 248 px | Collapsible to 72 px |
| Right Copilot panel | 340-400 px | Collapsible / overlay on smaller screens |
| Main workspace | Flexible | Takes remaining width |
| Status bar height | 32-40 px | Optional on small screens |
| Page horizontal padding | 24 px desktop, 16 px tablet/mobile | Consistent page rhythm |
| Card radius | 16-20 px | Enterprise SaaS style |
| Dense table row height | 56-64 px | Claims and channel queues |

### 5.3 Responsive Rules

| Breakpoint | Behavior |
|---|---|
| Desktop >= 1280 px | Three panels visible by default |
| Laptop 1024-1279 px | Left nav collapsible; right Copilot can remain visible if space allows |
| Tablet 768-1023 px | Left nav becomes drawer; Copilot becomes slide-over panel |
| Mobile < 768 px | Single-column workflow; Copilot and filters become bottom sheet or tabs |

### 5.4 Top Bar Requirements

The top bar must include:

- Product mark / IIP label
- Current workspace name
- Environment badge: Local, Dev, Staging, Production, Demo
- Tenant/workspace selector where applicable
- Global search
- Notifications / alerts
- User name and role
- Profile menu
- Help/support shortcut

### 5.5 Status Bar Requirements

The status bar should display:

- Connection state
- Last sync time
- Active workflows count
- Queue depth
- Provider health summary
- Integration failure indicator
- Current environment

Examples:

```text
Connected | Last sync: 09:47 | Active agents: 4 | Queue: 23 items | Providers healthy
```

---

## 6. Visual Design System

> **Ambient Intelligence theme update (2026-07-04, DEC-011).** Section 6 below is superseded by the Ambient Intelligence theme defined in `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` v1.2. The authoritative token names/values live there and in `web/portal/app/globals.css`; this section is kept for narrative context and updated to match. The change is skin-only — brand *feel* (premium, calm, enterprise, boardroom-ready) is unchanged.

### 6.1 Brand Direction

The visual language should feel:

- Premium
- Enterprise-grade
- Microsoft-native
- Calm
- Clean, with soft translucent (glass) surfaces over an ambient gradient background
- Boardroom-ready
- Operationally serious
- AI-enabled but not gimmicky

### 6.2 Core Color Tokens

Ambient Intelligence palette (blue→violet gradient family). Authoritative token names are the `--color-*` set in `web/portal/app/globals.css`; the reference hexes below are the design source values from the approved Lovable prototype.

```css
:root {
  /* Base ink & surfaces */
  --iip-ink: #0B0F3B;           /* base ink (was navy #040458/#0F172A) */
  --iip-bg-app: #FBFAFF;        /* ambient page background base */
  --iip-bg-panel: #FFFFFF;      /* glass panels layer over the ambient bg */
  --iip-border-soft: #E2E8F0;
  --iip-text-primary: #0B0F3B;
  --iip-text-muted: #64748B;

  /* Brand / accent gradient family */
  --iip-primary: #2563FF;       /* info blue — primary action */
  --iip-accent: #7C4DFF;        /* violet — AI accent */
  --iip-gradient-primary: linear-gradient(135deg, #2563FF 0%, #7C4DFF 100%);
  --iip-gradient-ambient:
    radial-gradient(1200px 600px at 10% -10%, #EEF2FF 0%, transparent 60%),
    radial-gradient(900px 500px at 110% 10%, #F3E8FF 0%, transparent 55%),
    radial-gradient(800px 500px at 50% 120%, #ECFDF5 0%, transparent 60%);

  /* Semantic status (meanings unchanged) */
  --iip-success: #059669;
  --iip-warning: #D97706;
  --iip-danger:  #DC2626;
  --iip-info:    #2563EB;

  /* Glass & depth */
  --iip-shadow-glass: 0 8px 32px rgba(31, 38, 135, 0.10);
}
```

### 6.3 Gradient and Accent Use

The blue→violet primary gradient (`--iip-gradient-primary`) is reserved for: primary CTAs, active navigation indicators, and key AI-accent moments. It must not be used as a dominant fill behind body content or dense tables (use plain glass surfaces there). The prior orange brand accent (`#FAA51A`) is retired from the default theme; white-labeled insurer deployments may re-introduce a single brand accent through tokens without changing layout.

### 6.4 Component Style

| Element | Style |
|---|---|
| Cards | Glass surface (`.glass`/`.glass-subtle`), 16–24 px radius, `--iip-shadow-glass`, `hover-lift` on interactive cards |
| Tables | Dense but readable on a glass panel, sticky header where useful, row hover states |
| Badges | Soft backgrounds, clear text, semantic color coding |
| Buttons | Primary = gradient (`--iip-gradient-primary`) with white text; secondary = translucent/outline; destructive red |
| Inputs | Rounded 12 px, light border, clear focus ring |
| Alerts | Semantic colors with concise actionable copy |
| AI cards | Soft violet tint (`--color-surface-ai`), evidence and confidence visible |
| Approval cards | Amber attention state until reviewed |
| Audit cards | Neutral, immutable, chronological |
| Page background | `.ambient-bg` (ambient gradient wash) behind the app shell |

---

## 7. Global Interaction Patterns

### 7.1 Loading States

Every page and major component must have a loading state.

Use:

- Skeleton cards for dashboards
- Skeleton rows for tables
- Spinner only for short inline operations
- Disabled buttons during submission
- Progress steps for document extraction workflows

Avoid blank screens.

### 7.2 Empty States

Empty states must explain what happened and what to do next.

Example:

```text
No claims match the current filters.
Try changing the status, SLA, product line, or assigned officer filter.
```

### 7.3 Error States

Error states must be helpful and safe.

Include:

- What failed
- Whether data may be stale
- Retry action
- Support link or correlation ID where relevant

Example:

```text
Claims could not be loaded.
The API returned an error. Retry or contact support with correlation ID: abc-123.
```

### 7.4 Unauthorized States

Unauthorized states should not expose sensitive data.

Example:

```text
You do not have permission to approve settlements above your approval limit.
This attempt has been logged for audit visibility.
```

### 7.5 Not Configured States

Because IIP depends on integrations and providers, `Not configured` states are required.

Examples:

- WhatsApp not configured
- Azure OpenAI not configured
- Document Intelligence not configured
- Microsoft Graph not configured
- AI Search index not configured
- Claims system integration not configured

Each state should show:

- What is missing
- Who can configure it
- Link to Integrations/Admin Settings
- Whether the feature is unavailable or degraded

---

## 8. AI and Copilot UX Rules

### 8.1 Global Assistant vs Contextual Copilot

IIP should support two AI surfaces:

| AI surface | Purpose |
|---|---|
| Global Assistant | Cross-platform search, help, workflow navigation, general operational questions |
| Contextual Copilot | Screen-specific intelligence for claims, documents, customers, leads, policies, approvals, and audit |

### 8.2 AI Output Anatomy

Every AI output card should show:

- Output title
- Short summary
- Confidence level
- Evidence/source references
- Missing data warnings
- Suggested next action
- Required human review indicator
- Audit status

Example:

```text
AI-prepared claim summary
Confidence: 87%
Evidence: Claim form, Police report, Repair quotation
Missing: Vehicle photos, Assessor report
Next action: Request missing documents
Human review: Required before customer update
```

### 8.3 Confidence Display

Confidence should be visible but not overemphasized.

| Confidence | UI treatment |
|---|---|
| 90-100% | Green/neutral confidence indicator |
| 70-89% | Blue/amber indicator, review still required |
| 50-69% | Amber warning, human review required |
| < 50% | Red/attention state, do not use without correction |
| Unknown | Not scored / evidence unavailable |

### 8.4 Prohibited AI UX

Do not show buttons or labels such as:

- Approve by AI
- Reject by AI
- Confirm fraud
- Auto-settle
- Auto-bind policy
- AI final decision

Use instead:

- Prepare approval pack
- Draft decision note
- Flag for review
- Create approval task
- Send to supervisor
- Request human review

---

## 9. Screen Specifications

## 9.1 Login and Tenant Selection

### Purpose

Authenticate internal insurer users and place them into the correct tenant/workspace context.

### Required UI

- IIP logo/name
- Email or Entra ID sign-in
- MFA challenge where configured
- Tenant selector where user belongs to multiple tenants
- Environment label
- Privacy/terms links
- System health note

### States

| State | Behavior |
|---|---|
| Loading | Show authentication progress |
| Failed login | Show generic error, do not reveal account existence |
| MFA required | Show MFA input/modal |
| Multiple tenants | Show tenant selector |
| Unauthorized tenant | Show safe access denied message |

### Acceptance Criteria

- User cannot access app without valid session.
- Tenant/workspace context is established before app loads.
- Role is fetched from server-side authorization model.
- User sees only allowed navigation and actions.

---

## 9.2 Command Center

### Purpose

The Command Center is the daily operational home for IIP. It should show what matters now and allow users to ask, search, filter, and act from one place.

### Layout

- Left: role-aware navigation
- Center: AI-first summary and operational cards
- Right: Today summary, suggested actions, risk/SLA signals

### Required KPI Cards

| KPI | Description |
|---|---|
| Open claims | Total active claim workload |
| New claims today | Newly received claims |
| Incomplete claims | Claims missing required documents |
| SLA at risk | Claims approaching SLA breach |
| SLA breached | Claims already overdue |
| Pending approvals | Human approval tasks awaiting action |
| Customer update backlog | Updates drafted or pending approval |
| AI questions answered | AI support activity volume |
| Escalations required | High-risk or blocked items |
| Average completeness score | Average document/case completeness |
| AI cost / usage | AI and document processing cost trend |
| AI quality trend | Confidence, correction rate, error rate |

### Suggested Questions

- Show me claims at risk of SLA breach.
- Which claims are missing police reports?
- What needs supervisor approval today?
- Summarize today's new motor claims.
- Show customer updates pending approval.
- What integrations are failing?

### Right Copilot Panel

Show:

- Workload summary
- Priority actions
- SLA risk
- AI quality/cost signals
- Integration/provider health
- Recommended next action

### Acceptance Criteria

- Metrics are role-aware.
- Each KPI links to the relevant filtered workspace.
- AI suggestions do not bypass human review.
- Loading, empty, error, unauthorized, and not-configured states are handled.

---

## 9.3 Channels Inbox

### Purpose

Channels is the insurer's internal unified inbox for customer-originated conversations across WhatsApp, WebChat, email, SMS, Teams, broker, call center, and website forms.

### Important Rule

Customers do not log into this workspace. Their external interactions are converted into internal conversation records, leads, claims, tasks, or work items.

### Required Data Fields

| Field | Description |
|---|---|
| Customer | Customer name or unknown customer label |
| Channel | WhatsApp, webchat, email, SMS, broker, call center, website form |
| Intent | Claim, quote, renewal, complaint, policy inquiry, document upload, payment inquiry, general service |
| AI confidence | Confidence in intent/routing |
| Suggested routing team | Claims, Sales, Customer Service, Compliance, Underwriting |
| Status | New, assigned, in review, waiting customer, waiting officer, converted, closed, escalated |
| Assigned officer | Staff member responsible |
| Last message preview | Safe preview of conversation |
| Last activity | Timestamp |
| Work item ID | Linked claim, lead, task, or conversation |

### Required Filters

- Channel
- Status
- Intent
- Assigned to
- Search
- Date range
- SLA/risk where applicable

### Required Metrics

- Active conversations
- New conversations today
- Failed channel events
- Average response time
- Conversations waiting customer
- Conversations waiting officer
- Escalated conversations

### Channel Health Panel

Show health per channel:

- Connected
- Degraded
- Disconnected
- Not configured
- Active conversations
- Failed events
- Last sync time

### Expected Endpoint

```text
GET /api/v1/channels/overview
```

### Acceptance Criteria

- Channel records come from real API responses, not mock data.
- Empty API results show empty state.
- Failed API calls show error state.
- Users can open a conversation or linked work item.
- Low-confidence routing requires human review.

---

## 9.4 Sales & Leads Inbox

### Purpose

Sales & Leads captures acquisition work created by external channels. It supports customer acquisition, product interest, lead qualification, product-fit review, quote readiness, and human follow-up.

### Required Lead Sources

- WhatsApp
- WebChat
- Broker
- Agent
- Call center
- Email
- Website form
- Bancassurance partner

### Required Lead Fields

| Field | Description |
|---|---|
| Lead reference | Unique lead ID |
| Customer display name | Person/business interested in insurance |
| Primary contact | Phone/email/contact handle |
| Source | Origin channel |
| Product interest | Motor, life, health, business, agriculture, travel, property, unknown |
| Status | New, qualifying, missing information, quote in progress, quote sent, converted, lost, closed |
| AI confidence | Product fit / qualification confidence |
| Product-fit summary | AI-prepared summary of likely need |
| Required information count | Missing items before quote/action |
| Next action | Human-owned next step |
| Assigned to | Sales officer / broker / user |
| Quote value | Estimated value where available |
| Last activity | Timestamp |

### Required Metrics

- New leads
- Qualified leads
- Quote in progress
- Converted leads
- Lead conversion rate
- Pipeline value
- Leads missing information
- Source/channel performance

### Expected Endpoint

```text
GET /api/v1/sales-leads/overview
```

### Acceptance Criteria

- Customer acquisition is represented as internal work, not an external app login.
- AI can recommend product fit but not bind policy.
- Human owner is visible for every lead or unassigned state.
- Missing information is actionable.

---

## 9.5 Customer 360

### Purpose

Customer 360 provides an internal consolidated customer view across policies, claims, leads, interactions, documents, consent, payments, and service history.

### Important Rule

This is not the customer portal. It is an internal insurer workspace.

### Required Customer Fields

| Field | Description |
|---|---|
| Display name | Customer name |
| Primary phone | Main phone number |
| Email | Email if available |
| National ID masked | Masked ID only, where applicable |
| Segment | Retail, SME, corporate, broker-managed, bancassurance, microinsurance |
| Lifecycle status | Active, prospect, lapsed, dormant, restricted |
| Active policies | Count |
| Open claims | Count |
| Open leads | Count |
| Unresolved complaints | Count |
| Consent status | Complete, partial, missing |
| Last interaction | Timestamp |

### Required Customer Profile Sections

- Identity and contact
- Policies
- Claims
- Leads/opportunities
- Channel conversations
- Documents
- Consent and privacy
- Payments and balances where configured
- Complaints
- Tasks
- AI customer summary
- Recommended next action

### Expected Endpoint

```text
GET /api/v1/customers/overview
```

### Acceptance Criteria

- Sensitive fields are masked by default.
- Access is role-aware.
- Consent gaps are visible.
- Customer summaries distinguish facts from AI-prepared summaries.

---

## 9.6 Claims Work Queue

### Purpose

The Claims Work Queue is the main operational queue for claims officers and supervisors.

### Required Columns

| Column | Description |
|---|---|
| Claim ID | Unique claim reference |
| Customer | Claimant/insured |
| Policy | Policy number or status |
| Product line | Motor, health, life, property, etc. |
| Claim type | Comprehensive motor, third-party, etc. |
| Status | Current workflow status |
| Completeness | Required documents received vs missing |
| SLA | On track, at risk, breached |
| Risk signal | None, advisory, high, escalated |
| AI summary status | Not generated, generated, needs review |
| Assigned officer | Claim owner |
| Last activity | Timestamp |
| Next action | Required human or system action |

### Required Filters

- Status
- Product line
- Claim type
- Risk level
- SLA status
- Channel
- Assigned officer
- Completeness
- Date range
- Search by customer, claim ID, policy number

### Required Actions

- Open claim
- Assign/reassign
- Escalate
- Request documents
- Generate/review summary
- Export claim pack
- Create approval task

### Acceptance Criteria

- Queue supports dense table layout.
- Claims at SLA risk are visually distinct.
- AI risk signals are advisory only.
- Fraud labels must say `signal`, not `confirmed fraud`.

---

## 9.7 FNOL / New Claim Intake

### Purpose

FNOL captures First Notice of Loss from WhatsApp, email, web, broker, call center, or internal staff entry.

### Required Intake Steps

```text
Select or identify customer
↓
Confirm or search policy
↓
Capture incident details
↓
Select claim type
↓
Upload or request documents
↓
Create claim reference
↓
Generate missing-document checklist
↓
Route to claims queue
↓
Send customer-safe acknowledgement after approval/configured rule
```

### Required Motor FNOL Fields

- Customer name
- Customer contact
- Policy number
- Vehicle registration
- Accident date/time
- Accident location
- Incident description
- Driver name
- Driving permit number
- Third party involved
- Injury indicator
- Police report status
- Uploaded documents
- Preferred communication channel

### Acceptance Criteria

- FNOL can be created from channel conversation.
- System can generate claim reference.
- Required documents are based on claim type.
- Customer communication is customer-safe and auditable.

---

## 9.8 Case Review

### Purpose

Case Review is the central intelligence object for each claim.

### Required Sections

- Claim header
- Status and SLA
- AI claim summary
- Customer details
- Policy details
- Incident timeline
- Documents received
- Missing documents
- Extracted fields
- Evidence panel
- Fraud/advisory signals
- Notes
- Communications
- Tasks
- Human decision panel
- Approval history
- Audit trail

### Claim Header Should Show

- Claim ID
- Status
- SLA state
- Claim type
- Policy number
- Customer
- Assigned officer
- Created date
- Last activity
- Risk signal

### Human Decision Panel

The decision panel must distinguish:

- AI-prepared recommendation
- Staff decision
- Supervisor approval
- System automation
- Customer action
- Export/write-back

### Prohibited Actions

Do not expose direct actions that imply autonomous final decisioning.

Allowed:

- Prepare approval pack
- Create recommendation note
- Send to supervisor
- Draft customer update
- Request missing document
- Mark extraction reviewed

Requires human approval:

- Approve claim
- Reject claim
- Approve settlement
- Release payment
- Send final decision letter

---

## 9.9 Document Checker and Extraction Review

### Purpose

The Document Checker turns uploaded documents into structured, reviewable evidence.

### Required Document States

- Required
- Uploaded
- Accepted
- Rejected
- Unreadable
- Expired
- Mismatched
- Duplicate suspected
- Low confidence
- Missing

### Review Screen Layout

```text
Left: Document viewer
Center/right: Extracted fields
Bottom/side: Evidence, confidence, validation messages, reviewer actions
```

### Required Field Review Actions

- Approve extracted field
- Edit extracted field
- Reject extracted field
- Mark low confidence
- Link to evidence
- Request re-upload
- Add reviewer note

### Required Validation Examples

- Police report date does not match claim form incident date.
- Vehicle registration differs across documents.
- Driving permit is expired.
- National ID image is blurry.
- Repair quotation is missing date or stamp.
- Document appears to be the wrong type.

### Acceptance Criteria

- Low-confidence fields must require review.
- Original document is always preserved.
- Human corrections are logged.
- Extracted fields link back to source evidence.

---

## 9.10 Knowledge Base

### Purpose

The Knowledge Base stores approved policy, claims, underwriting, FAQ, SOP, template, and rule documents for RAG and internal support.

### Required Features

- Upload document
- Assign document type
- Assign product line
- Assign tenant/workspace
- Approve for AI use
- Archive document
- Index document
- Test retrieval
- View citations
- View version history

### Knowledge States

- Draft
- Pending approval
- Approved
- Indexed
- Archived
- Failed indexing
- Expired

### Acceptance Criteria

- AI cannot answer policy questions from unapproved documents.
- Policy answers must cite approved sources.
- Expired documents are excluded or flagged.
- Knowledge manager approval is required before indexing for customer-impacting answers.

---

## 9.11 Approvals

### Purpose

Approvals centralizes human review tasks for sensitive, consequential, or controlled actions.

### Approval Types

- Claim decision approval
- Settlement approval
- Customer communication approval
- Document correction approval
- Fraud investigation escalation
- Underwriting referral
- Export/write-back approval
- High-value claim approval

### Approval Card Requirements

- Requested action
- Requesting user/agent
- Claim/customer/policy context
- Evidence summary
- AI rationale where applicable
- Confidence/uncertainty
- Risk level
- SLA timer
- Approve/reject/escalate/request more info
- Audit trail link

### Acceptance Criteria

- Approvers see evidence before approving.
- Approvals are immutable once completed except through explicit override process.
- Every approval stores user, timestamp, rationale, evidence IDs, and outcome.

---

## 9.12 Compliance and Audit

### Purpose

Compliance & Audit provides regulator-ready visibility over AI actions, human actions, access events, approvals, overrides, customer communications, and exports.

### Required Audit Filters

- Case/claim ID
- Customer
- User
- Role
- Agent
- Date/time
- Action type
- Decision type
- Evidence used
- Status
- Exported/not exported

### Required Audit Event Types

- User login
- Data access
- Document uploaded
- Document classified
- Field extracted
- Field edited
- Field approved
- AI summary generated
- AI output corrected
- Missing document requested
- Claim escalated
- Approval requested
- Approval completed
- Customer communication drafted
- Customer communication sent
- Export generated
- Write-back attempted
- Integration failed

### Acceptance Criteria

- Audit logs are append-only from the user perspective.
- Users cannot hide AI output or human overrides.
- Audit export is available for authorized compliance users.

---

## 9.13 Reports and Analytics

### Purpose

Reports & Analytics gives business users insight into operational performance, AI impact, SLA compliance, cost, quality, and ROI.

### Required V1 Reports

- Claims volume by status
- Claims SLA status
- Missing document bottlenecks
- Claims turnaround time
- Claims by channel
- AI summary usage
- Document extraction confidence
- Human correction rate
- Pending approvals
- Customer update backlog
- Channel volume
- Lead conversion status
- Integration failures

### AI Insight Layer

Reports should include short explanations, not only charts.

Example:

```text
Motor claims delays are mostly caused by missing police reports and assessor reports. Most SLA risk is concentrated in the assessment stage.
```

### Acceptance Criteria

- Reports are role-aware.
- Metrics link back to underlying records where permitted.
- AI interpretation must not invent causal claims without evidence.

---

## 9.14 Integrations

### Purpose

The Integrations workspace manages connection status, configuration, health, and operational readiness for external systems.

### Required Integrations

| Integration | V1 status |
|---|---|
| WhatsApp Business API | Required or not-configured state |
| Microsoft Graph / Outlook | Required or not-configured state |
| Azure Blob Storage | Required |
| Azure AI Document Intelligence | Required |
| Azure AI Search | Required |
| Azure OpenAI | Required |
| Policy admin system | Optional/read-only/phase-dependent |
| Claims management system | Optional/phase-dependent |
| SMS gateway | Later |
| Power BI/Fabric | Later/basic placeholders |

### Required Integration Health States

- Connected
- Degraded
- Disconnected
- Not configured
- Misconfigured
- Permission issue
- Quota exceeded
- Sync delayed

### Acceptance Criteria

- Failed integrations show clear cause where safe.
- Secrets are never displayed.
- Config changes require admin role.
- Provider health is visible in Support & Health and relevant feature screens.

---

## 9.15 Admin Settings

### Purpose

Admin Settings allow insurer admins and platform admins to configure tenant behavior without code changes.

### Required Settings Areas

- Tenant profile
- Workspaces/branches/product lines
- Users and roles
- Approval thresholds
- Claim types
- Document requirements
- Workflow stages
- SLA rules
- Notification templates
- Consent templates
- AI provider settings
- Knowledge settings
- Integration settings
- Retention rules
- Support access

### Acceptance Criteria

- Settings changes are audited.
- Dangerous changes require confirmation.
- Admins cannot grant permissions beyond their own authority.
- Not-configured states link to relevant settings page.

---

## 9.16 Support and Health

### Purpose

Support & Health provides operational visibility for engineering, platform admins, and customer support.

### Required Health Indicators

- API health
- Database health
- Queue backlog
- Document processing backlog
- AI provider health
- Search index status
- Blob storage status
- Graph/Outlook status
- WhatsApp status
- Recent failures
- Deployment version
- Last successful sync
- Correlation ID search

### Acceptance Criteria

- Health data is not exposed to unauthorized users.
- Platform admins can diagnose integration/provider failures.
- Support engineers can trace failed workflows by correlation ID.

---

## 9.17 Channels Inbox — Threaded + Actions (parity amendment)

> Amends 9.3. Brings Channels to full workflow parity with the Lovable reference. DEC-012 / Initiative LP.2.

### Purpose

A unified, **threaded** inbox across configured channels (WhatsApp, Email/Outlook, Web chat, SMS, Broker) where a staff user reads a full conversation timeline and takes governed next actions — not just a flat conversation list.

### Required UI

- Two-pane layout: conversation list (left) + selected conversation thread (right), plus the existing channel-health panel.
- Thread view shows the ordered message timeline, detected intent, AI confidence (text + visual), matched customer (or "no match"), and suggested routing.
- Governed action bar on the thread: **Create claim**, **Create lead**, **Draft response**, **Escalate** — each opens the shared `GovernedActionDialog` (reason capture + audit), never executes silently. Create-claim reuses the FNOL/new-case intake; Draft response reuses the Customer Update draft→approve→send flow.

### Important Rule

Nothing is auto-actioned. Every action produces a human-reviewable artifact (a draft, a new case in intake state, an escalation record). Conversation ingestion is live-provider dependent: until a channel provider is configured and ingesting, the inbox shows an empty state, never sample conversations.

### Expected Endpoints

- `GET /api/v1/channels/overview` (existing) — conversation list.
- `GET /api/v1/channels/conversations/{id}` — thread detail (new).
- `POST /api/v1/channels/conversations/{id}/create-claim | create-lead | draft-response | escalate` (new, governed, audited).

### Acceptance Criteria

- Actions are gated by backend `ActionStateContract`; blocked actions show a reason.
- All four actions write audit events with correlation IDs.
- Tenant/workspace scoping enforced server-side; no cross-tenant conversations.

---

## 9.18 Customer 360 Detail

> Extends 9.5 (list/profile) with a dedicated detail route. DEC-012 / Initiative LP.2.

### Purpose

A single customer profile page (`/customers/{customerId}`) aggregating everything known about a customer for human review.

### Required Sections

- Header: name, key identifiers, consent status, workspace.
- Tabs / sections: Policies, Claims, Documents, Interactions (channel history), Consent log.
- **AI summary panel**: a grounded, cited summary of the customer's current situation — must show source citations (reuse Policy Intelligence's `SourceCitation`) and the "prepared for human review" disclaimer; renders `Not configured` if grounding sources are unavailable rather than summarizing from model memory.
- **Next best action**: a suggested (non-executing) next step, presented as a proposal reusing the Command Center action-proposal pattern.

### Expected Endpoint

- `GET /api/v1/customers/{customerId}` (existing) — extended with an AI-summary (cited) field and a next-best-action field.

### Acceptance Criteria

- No fabricated policy/claim/consent data; unmodeled sections show empty/`Not configured`.
- AI summary never appears as a decision; citations required or the panel degrades honestly.
- Server-side tenant scoping; viewing a customer writes an audit event.

---

## 9.19 Communications Hub

> New. Generalizes the per-case Customer Update queue into a cross-case queue. DEC-012 / Initiative LP.2.

### Purpose

A single queue of outbound customer communications across all cases, filterable by delivery status, for review/approval/tracking.

### Required Data Fields

- Customer, case reference, channel, drafted-by (AI/human), created time, last status change.
- Delivery status (enum): **Pending Approval**, **Approved**, **Sent**, **Send Failed**, **Rejected**.

### Required Actions

- Approve, Reject, Send (governed via existing Customer Update dialog); Send is provider-dependent and shows `Not configured` where the sending channel isn't wired.

### Expected Endpoint

- `GET /api/v1/communications` — cross-case list with status filter (new); existing per-case customer-update approve/reject/send endpoints reused for actions.

### Acceptance Criteria

- Status is a real enum end-to-end (no free-text status).
- Every state change audited; customer-safe content only in previews.

---

## 9.20 Documents Hub

> New cross-case hub; complements 9.9 (per-case Document Checker). DEC-012 / Initiative LP.2.

### Purpose

A cross-case view of all documents and their processing lifecycle, with retry.

### Required Data Fields

- Document type, source channel, case reference, uploaded time, reviewer.
- Processing status (enum): Uploaded, Extraction Pending (Processing), Extracted (Processed), **Review Required**, **Reviewed**, Extraction Approved, **Rejected**, Failed.
- Evidence ID, extraction confidence.

### Required Actions

- Open per-case Document Checker (drill-in), **Retry extraction** (governed), Reject.

### Expected Endpoints

- `GET /api/v1/documents` — cross-case list (new).
- `POST /api/v1/cases/{caseId}/documents/{docId}/retry-extraction` (new).

### Acceptance Criteria

- Status enum extended with ReviewRequired/Reviewed/Rejected end-to-end.
- Retry is idempotent and audited; provider-dependent extraction shows `Not configured` when Document Intelligence isn't wired.

---

## 9.21 Fraud Intelligence

> New. Rule-based v1. DEC-013 / DEC-014 / Initiative LP.4.

### Purpose

Surface **advisory** fraud signals computed deterministically from real case/customer data, each linked to evidence, for human investigation.

### Important Rule

The platform never states "fraud confirmed." Output is limited to advisory signals ("Advisory signal", "Requires investigation", "Potential anomaly") with the evidence that triggered them. No accusation of any customer, provider, broker, or staff member. This enforces `AGENTS.md` §4.

### Required UI

- Signal list with: signal type, linked case/customer, triggering rule, evidence links, advisory severity (text + visual), status (Open / Under investigation / Dismissed).
- Signal detail showing the exact rule and the data that matched (auditable).

### Expected Endpoint

- `GET /api/v1/fraud/signals` (new) — tenant-scoped advisory signals.

### Acceptance Criteria

- Wording never reads as a verdict anywhere.
- Each signal traces to a deterministic rule + evidence (no black-box score presented as truth).
- Rule-based v1; a future Azure OpenAI narration layer (Gate 3, DEC-014) may explain signals but not replace the rule engine.

---

## 9.22 Underwriting Intelligence

> New. Rule-based v1. DEC-013 / DEC-014 / Initiative LP.5.

### Purpose

Assist underwriting review by surfacing rule-based checks and drafting clarifying questions — never deciding acceptance/rejection or setting premium.

### Important Rule

Persistent banner: "AI prepares underwriting work; it does not decide acceptance, rejection, or premium." Action labels limited to: Draft questions, Summarize, Escalate.

### Required UI

- Per-submission checklist of product-line/coverage/limit rule results (pass / attention / missing information).
- Missing-information panel; drafted clarifying questions (human-editable, non-sent).

### Expected Endpoint

- `GET /api/v1/underwriting/reviews/{id}` (new) — rule results + drafted questions.

### Acceptance Criteria

- No accept/reject/bind/premium action exists in the UI.
- All checks are deterministic and traceable; missing data shows honestly.

---

## 9.23 AI Agent Registry

> New. Admin control plane. DEC-013 / ADR-0002 / Initiative LP.3.

### Purpose

Admin surface (`/admin/agents`) to view and govern the AI agents the platform runs.

### Required UI

- Registry table: agent name, purpose, current prompt version, enabled/disabled (kill switch) state, confidence threshold, human-approval requirement, last run.
- Agent detail drawer: prompt/version history, tool + knowledge policy, confidence threshold, human-approval rules, run logs (real runs only), usage/cost.
- **Kill switch panel**: per-agent and tenant-wide disable, via `GovernedActionDialog` (reason + audit). Disabling fails the agent closed at invocation (`Disabled by administrator`), never a fabricated fallback.

### Expected Endpoints

- `GET /api/v1/admin/agents`, `GET /api/v1/admin/agents/{id}` (new).
- `POST /api/v1/admin/agents/{id}/kill-switch` (new, governed, audited).

### Acceptance Criteria

- Kill switch enforced at agent-invocation time; unit-tested to fail closed.
- No registry setting can bypass the human-supervised decision boundary (ADR-0002).
- All state tenant-scoped; run logs never fabricated.

---

## 9.24 Billing & Entitlements

> New. Admin surface. DEC-013 / ADR-0003 / Initiative LP.6.

### Purpose

Admin surface (`/admin/billing`) showing what the tenant is entitled to and its real usage.

### Required UI

- Package / module entitlements, seat count vs. seats used.
- Usage meters (real, computed): AI calls, documents processed, active channels, storage — each vs. its entitlement limit, with an overage indicator.
- **Upgrade request** (governed, audited internal request — does not transact).

### Expected Endpoint

- `GET /api/v1/admin/billing` (new) — entitlements + computed usage.

### Acceptance Criteria

- All usage numbers computed from real internal counters; unmetered dimensions show `Not configured`, never fabricated zeros/samples.
- Tenant-scoped; entitlements gate feature availability but never replace role/permission checks.
- No external billing/payment integration in v1 (ADR-0003).

---

## 9.25 Workspaces

> New. Workspace switching. DEC-012 / Initiative LP.2.

### Purpose

Let a user with access to multiple workspaces switch the active workspace context.

### Required UI

- Workspace switcher in the Top Bar, listing the workspaces the authenticated user can access (from `/api/v1/me`).
- Switching updates the active workspace scope used by all subsequent tenant-scoped requests.

### Expected Endpoint

- `GET /api/v1/me` (existing) — must expose the user's available workspaces (add the field if absent).

### Acceptance Criteria

- Only workspaces the user is authorized for appear; selection never trusted client-side without server validation.
- Switching re-scopes data correctly; no cross-workspace leakage.

---

## 10. Role-Based UX Requirements

### 10.1 Role Navigation Matrix

| Screen | Claims Officer | Supervisor | Customer Service | Underwriter | Compliance | Insurer Admin | Executive | Otic Admin |
|---|---|---|---|---|---|---|---|---|
| Command Center | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| Channels | View/act assigned | View/escalate | Yes | Limited | Audit view | Configure | Summary | Support |
| Sales & Leads | Limited | Summary | Yes | Limited | Audit view | Configure | Summary | Support |
| Customers | Assigned/limited | Team view | Yes | Limited | Audit view | Configure | Summary | Support |
| Claims | Yes | Yes | Limited | Limited | Audit view | Configure | Summary | Support |
| Documents | Yes | Yes | Limited | Yes | Audit view | Configure | Summary | Support |
| Approvals | Request | Approve | Request | Request/approve if allowed | Review | Configure | Summary | Support |
| Compliance & Audit | Limited | Limited | Limited | Limited | Yes | Yes | Summary | Support |
| Reports | Limited | Yes | Limited | Limited | Yes | Yes | Yes | Support |
| Integrations | No | No | No | No | View | Configure | View | Configure/support |
| Admin Settings | No | Limited | No | No | View | Yes | View | Support |
| Support & Health | No | Limited | No | No | View | Yes | Summary | Yes |

### 10.2 Permission Categories

Use permissions more granular than roles.

Examples:

- View claim
- Edit claim
- Assign claim
- Escalate claim
- Approve claim decision
- Approve settlement
- View sensitive documents
- Export claim pack
- Configure workflow
- Configure integration
- View audit logs
- Manage users
- Approve knowledge document
- Send customer communication

---

## 11. Status and Badge System

### 11.1 Claim Statuses

- New
- Intake started
- Documents pending
- Documents uploaded
- AI processing
- Needs review
- Missing information
- Fraud signal detected
- Escalated
- Approved for next workflow step by human
- Returned to customer
- Exported
- Closed

### 11.2 SLA Statuses

| Status | Meaning |
|---|---|
| On track | No immediate SLA risk |
| At risk | Approaching SLA deadline |
| Breached | SLA deadline passed |
| Paused | Waiting customer/document/external dependency |
| Not applicable | SLA not configured |

### 11.3 AI Output Statuses

- Not generated
- Generated
- Needs review
- Reviewed
- Corrected by human
- Rejected by human
- Low confidence
- Evidence missing

### 11.4 Document Statuses

- Required
- Uploaded
- Accepted
- Rejected
- Unreadable
- Expired
- Mismatched
- Duplicate suspected
- Low confidence
- Missing

---

## 12. API and Data Contract Assumptions

### 12.1 Known V1 Overview Endpoints

```text
GET /api/v1/channels/overview
GET /api/v1/customers/overview
GET /api/v1/sales-leads/overview
```

### 12.2 Required API Behavior

All list/overview endpoints should support:

- Pagination
- Filters
- Search
- Tenant/workspace context
- Role-aware results
- Empty response
- Error response
- Correlation ID

### 12.3 Frontend API Rules

- Do not use mock data in deployed staging/production.
- Empty API results render empty states.
- Failed API calls render error states.
- Unconfigured providers render not-configured states.
- API errors should show helpful copy and correlation IDs where available.
- Secrets must never be exposed to the frontend.

### 12.4 Tenant and Workspace Context

Every major API call should be scoped by:

- Tenant ID
- Workspace ID
- User ID
- Role/permission set
- Correlation ID

The frontend may pass context headers in local/dev where required, but production authorization must be server-side.

---

## 13. Accessibility and Usability Requirements

### 13.1 Accessibility

- Use semantic HTML.
- All controls must be keyboard reachable.
- Provide visible focus states.
- Maintain color contrast for text and badges.
- Do not rely on color alone for SLA/risk/approval status.
- Tables must have clear headers.
- Form errors must be associated with fields.
- Modals must trap focus and support Escape close where safe.

### 13.2 Usability

- Use plain insurance language.
- Avoid unnecessary icons and visual clutter.
- Explain statuses in context.
- Show next action on every operational object.
- Keep critical workflows within 1-3 clicks from Command Center.
- Preserve user context when moving between queue and detail view.

---

## 14. Implementation Guidance for Antigravity / Codex Agents

### 14.1 General Implementation Rules

- Use **Insurance Intelligence Platform (IIP)** naming everywhere.
- Do not reintroduce ACIP naming.
- Build internal insurer workspaces first.
- Do not create customer login for V1 unless explicitly required.
- Do not use mock data in staging/production.
- Preserve loading, empty, error, unauthorized, and not-configured states.
- Enforce authorization server-side.
- Log sensitive workflow actions.
- Keep AI outputs source-grounded and reviewable.
- Do not implement autonomous claim approval, claim rejection, fraud accusation, settlement approval, payment release, policy binding, or final underwriting decisioning.

### 14.2 Frontend Implementation Rules

- Use Next.js App Router.
- Use React and TypeScript.
- Use Tailwind CSS patterns aligned to IIP tokens.
- Keep the app shell consistent.
- Implement the three-panel layout as a reusable shell.
- Build role-aware navigation from server-provided permissions.
- Keep tables dense but readable.
- Use reusable cards, badges, drawers, modals, tables, and panels.
- Use TanStack Query or equivalent data-fetching pattern for loading/error/refetch states.

### 14.3 Backend Alignment Rules

- Use .NET 8 API as primary backend.
- Provide stable typed endpoints for frontend modules.
- Return role-filtered data.
- Include audit metadata where needed.
- Return correlation IDs on errors.
- Provide health endpoints for Support & Health.
- Keep provider integrations behind backend services.

### 14.4 AI UX Implementation Rules

- Display AI outputs as recommendations, summaries, or drafts.
- Always show evidence and confidence where applicable.
- Route sensitive outputs to human review.
- Allow humans to correct, reject, or override AI output.
- Log corrections for audit and future improvement.

---

## 15. Screen Acceptance Checklist

Each screen is complete only when it has:

- Correct IIP naming
- Role-aware navigation/action visibility
- Loading state
- Empty state
- Error state
- Unauthorized state where applicable
- Not-configured state where applicable
- Responsive behavior
- Keyboard accessibility
- API integration
- Audit logging for sensitive actions
- No mock data in deployed environment
- Human approval where required
- AI language that does not imply final autonomous decisioning

---

## 16. MVP UI Build Order

| Order | Build item | Reason |
|---:|---|---|
| 1 | Authenticated app shell | Foundation for all screens |
| 2 | Role-aware navigation | Prevents later rework |
| 3 | Command Center | Main demo and daily entry point |
| 4 | Channels Inbox | Feeds claims, leads, and service workflows |
| 5 | Sales & Leads Inbox | Supports customer acquisition pillar |
| 6 | Customer 360 | Centralizes customer context |
| 7 | Claims Work Queue | Core MVP operating surface |
| 8 | FNOL Intake | Starts claims workflow |
| 9 | Case Review | Main claims intelligence workspace |
| 10 | Document Extraction Review | Core document intelligence proof |
| 11 | Approvals | Enforces human-in-the-loop |
| 12 | Compliance & Audit | Supports governance and trust |
| 13 | Integrations | Shows production readiness |
| 14 | Support & Health | Shows operational readiness |
| 15 | Reports & Analytics | Adds executive visibility |
| 16 | Admin Settings | Enables configuration |

---

## 17. Open UX Questions

| ID | Question | Owner |
|---|---|---|
| UX-001 | Should the first pilot include a customer-safe portal, or only WhatsApp/web/email external flows? | Product / Leadership |
| UX-002 | Which roles should appear in the first demo: Admin, Manager, Officer, Approver, Compliance, Executive? | Product |
| UX-003 | Should the default Command Center view be claims-first or balanced across acquisition and claims? | Product / Sales |
| UX-004 | Which WhatsApp provider will be used: Meta direct, Twilio, Infobip, or another provider? | Engineering |
| UX-005 | Should Document Extraction Review use side-by-side PDF viewer in V1 or simplified extracted-fields view first? | Engineering / Design |
| UX-006 | Which exact insurer branding/white-labeling options are required for pilot? | Product / Sales |
| UX-007 | Which workflow actions require supervisor approval in the first pilot? | Claims SME / Product |
| UX-008 | What is the required claims register export format? | Compliance / Product |
| UX-009 | Should Power BI appear as live reports or placeholders in V1? | Product / Engineering |
| UX-010 | Should the right Copilot panel be always visible on desktop or user-toggleable by default? | Design |

---

## 18. Final Canonical UI Statement

> **IIP should feel like a Microsoft-native, AI-assisted insurance command center where every claim, customer, document, lead, and workflow is contextual, evidence-linked, role-aware, and human-reviewed. The AI should make work easier to understand and act on, but the interface must always make it clear that human staff own regulated insurance decisions.**

---

## 19. Immediate Handoff Actions

| # | Action | Owner |
|---:|---|---|
| 1 | Update UI labels to use Insurance Intelligence Platform (IIP), not ACIP | Frontend/Product |
| 2 | Implement reusable three-panel app shell | Frontend |
| 3 | Confirm role-aware navigation matrix | Product/Backend |
| 4 | Build Command Center with real API/empty/error states | Frontend/Backend |
| 5 | Implement Channels overview endpoint and page | Backend/Frontend |
| 6 | Implement Customers overview endpoint and page | Backend/Frontend |
| 7 | Implement Sales & Leads overview endpoint and page | Backend/Frontend |
| 8 | Refine Claims Work Queue and Case Review using this handoff | Product/Frontend |
| 9 | Build Document Extraction Review with evidence/confidence patterns | Frontend/AI/Backend |
| 10 | Add Approvals and Audit patterns before enabling sensitive actions | Backend/Frontend/Compliance |
| 11 | Add not-configured provider states for OpenAI, Document Intelligence, Search, Graph, WhatsApp | Frontend/Backend |
| 12 | QA every screen against the acceptance checklist | QA |

