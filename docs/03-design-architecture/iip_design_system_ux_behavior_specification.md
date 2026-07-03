---
Product: Insurance Intelligence Platform (IIP)
Document Name: Design System & UX Behavior Specification
Version: 1.1 - Chat-First Command Center Update
Status: Draft - updated for chat-first Command Center
Audience: Product, UX, Frontend Engineering, QA, Implementation Agents
Date: 2026-06-30
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

## Chat-First Command Center Design System Addendum

### Updated Command Center density rule

The Command Center density is **medium**, but the visual priority must now be:

1. central chat workspace;
2. embedded metric/action/result blocks;
3. evidence rail;
4. secondary navigation.

Metric cards must not dominate the first viewport. Use compact metric pills inside the daily briefing and agent responses.

### Command chat layout tokens

| Token | Value | Use |
|---|---:|---|
| `--iip-command-chat-max-width` | `1120px` | Preferred readable width for central chat content. |
| `--iip-command-composer-height` | `88px` | Sticky command composer minimum height. |
| `--iip-command-message-gap` | `16px` | Spacing between chat turns and embedded blocks. |
| `--iip-command-pill-height` | `44px` | Metric/action pill minimum height. |
| `--iip-evidence-rail-width` | `360px` | Right context/evidence rail. |

### Tailwind patterns for chat-first Command Center

| Pattern | Recommended classes |
|---|---|
| Command page shell | `flex min-h-0 flex-1 overflow-hidden bg-[var(--iip-bg-app)]` |
| Chat workspace | `flex min-h-0 flex-1 flex-col` |
| Chat scroll area | `custom-scrollbar min-h-0 flex-1 overflow-y-auto px-4 py-5 sm:px-6 lg:px-8` |
| Chat content width | `mx-auto w-full max-w-[var(--iip-command-chat-max-width)] space-y-4` |
| Assistant message card | `rounded-[var(--iip-radius-lg)] border border-[var(--iip-border-soft)] bg-white p-5 shadow-sm` |
| User message bubble | `ml-auto max-w-[720px] rounded-2xl bg-indigo-50 px-4 py-3 text-sm text-[var(--iip-text-primary)]` |
| Metric pill strip | `grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6` |
| Metric pill | `rounded-[var(--iip-radius-md)] border border-[var(--iip-border-soft)] bg-white px-3 py-3 shadow-sm hover:bg-slate-50` |
| Embedded result block | `rounded-[var(--iip-radius-lg)] border border-[var(--iip-border-soft)] bg-white shadow-sm` |
| Sticky composer | `border-t border-[var(--iip-border-soft)] bg-white/95 p-4 backdrop-blur` |
| Evidence rail | `hidden w-[var(--iip-evidence-rail-width)] shrink-0 border-l border-[var(--iip-border-soft)] bg-white xl:block` |

### Chat-first visual behavior

- The daily briefing should appear as an assistant message, not a separate dashboard header.
- Metric pills should be compact and clickable.
- Embedded operational blocks should look like work artifacts inside the conversation.
- The composer must remain visible at the bottom of the main workspace.
- The right rail should use white or soft-lavender surfaces, not a dark drawer.
- The left sidebar should remain light in MVP unless a separate approved design update changes it.

# Design System & UX Behavior Specification

## Purpose

This document defines the detailed design system and UX behavior rules for IIP. It extends the UI/UX Handoff Document into implementation-level guidance for screens, components, states, spacing, interaction behavior, and responsive design.

## Design Principles

| Principle | Implementation Meaning |
|---|---|
| Enterprise clarity | The interface must make claim state, SLA state, document completeness, and human responsibility obvious. |
| Chat-first assistance | The Command Center and Copilot panel should support conversational workflows without hiding operational controls. |
| Human accountability | Any AI output must show evidence, confidence, source context, and human-review status where relevant. |
| Dense but readable | Claims and document operations need high information density, but not cramped layouts. |
| Role-aware presentation | Users see actions and navigation appropriate to their role; permissions must still be enforced server-side. |
| No fake certainty | Empty, error, loading, unauthorized, and Not configured states must be explicit. |

## Confirmed Core Design Tokens

The following values are confirmed from the existing UI/UX handoff.

```css
:root {
  --iip-navy-950: #040458;
  --iip-navy-900: #101056;
  --iip-orange-500: #FAA51A;
  --iip-bg-app: #F8FAFC;
  --iip-bg-panel: #FFFFFF;
  --iip-bg-soft: #F1F5F9;
  --iip-border-soft: #DBE3EF;
  --iip-border-strong: #CBD5E1;
  --iip-text-primary: #0F172A;
  --iip-text-muted: #475569;
  --iip-text-disabled: #94A3B8;
  --iip-blue-600: #2563EB;
  --iip-cyan-600: #0891B2;
  --iip-success-bg: #ECFDF5;
  --iip-success-text: #047857;
  --iip-warning-bg: #FFFBEB;
  --iip-warning-text: #B45309;
  --iip-danger-bg: #FEF2F2;
  --iip-danger-text: #B91C1C;
  --iip-info-bg: #EFF6FF;
  --iip-info-text: #1D4ED8;
}
```

## Recommended Implementation Tokens

These tokens should be implemented as CSS variables or Tailwind theme extensions. Values not already confirmed should be treated as implementation defaults and may be tuned after visual QA.

| Token | Value | Use |
|---|---:|---|
| `--iip-radius-sm` | `6px` | Tags, small buttons, small inputs. |
| `--iip-radius-md` | `10px` | Cards, drawers, inputs, compact panels. |
| `--iip-radius-lg` | `14px` | Main panels, modals, large cards. |
| `--iip-shadow-card` | `0 1px 2px rgba(15, 23, 42, 0.06)` | Normal cards and panels. |
| `--iip-shadow-float` | `0 12px 30px rgba(15, 23, 42, 0.12)` | Modals, popovers, floating Copilot elements. |
| `--iip-transition-fast` | `120ms ease-out` | Button hover, row hover, badge transitions. |
| `--iip-transition-standard` | `180ms ease-out` | Drawer, modal, tab, state changes. |
| `--iip-shell-sidebar-width` | `280px` | Persistent left navigation on desktop. |
| `--iip-context-panel-width` | `360px` | Right Copilot/context panel on desktop. |

## Layout System

| Region | Desktop Behavior | Tablet Behavior | Mobile Behavior |
|---|---|---|---|
| Left navigation | Persistent, collapsible, role-aware. | Collapsible rail. | Hidden behind menu. |
| Top bar | Workspace, environment, global search, alerts, user/role. | Search may collapse into icon. | Search opens as full-screen or sheet. |
| Main workspace | Claim queues, Command Center, review screens. | Single main region with optional drawer. | Single-column stacked flow. |
| Right Copilot panel | Contextual, evidence-aware, not a decision owner. | Drawer or collapsible side sheet. | Bottom sheet or separate tab. |

## Tailwind Class Patterns

| Pattern | Recommended Classes |
|---|---|
| App shell | `min-h-screen bg-[var(--iip-bg-app)] text-[var(--iip-text-primary)]` |
| Main card | `rounded-[var(--iip-radius-lg)] border border-[var(--iip-border-soft)] bg-white shadow-sm` |
| Dense table row | `h-12 border-b border-[var(--iip-border-soft)] hover:bg-slate-50` |
| Primary button | `rounded-md bg-[var(--iip-navy-950)] px-4 py-2 text-white hover:bg-[var(--iip-navy-900)]` |
| Accent action | `rounded-md bg-[var(--iip-orange-500)] px-4 py-2 text-slate-950` |
| Muted text | `text-sm text-[var(--iip-text-muted)]` |
| Danger badge | `rounded-full bg-[var(--iip-danger-bg)] px-2 py-0.5 text-xs text-[var(--iip-danger-text)]` |
| Warning badge | `rounded-full bg-[var(--iip-warning-bg)] px-2 py-0.5 text-xs text-[var(--iip-warning-text)]` |
| Info badge | `rounded-full bg-[var(--iip-info-bg)] px-2 py-0.5 text-xs text-[var(--iip-info-text)]` |

## Component Behavior Rules

| Component | Required Behavior |
|---|---|
| Claim card | Show claim ID, customer, product line, status, SLA, risk indicator, assigned user, next action. |
| Document card | Show document type, source channel, extraction status, confidence, reviewer, missing/accepted/rejected state. |
| AI output panel | Show evidence links, confidence, last updated time, and clear disclaimer that human review is required. |
| Drawer | Preserve underlying page context; drawer must be dismissible and support deep-linkable record state where possible. |
| Modal | Use only for blocking confirmation or focused tasks; never hide critical evidence behind nested modals. |
| Toast | Use for confirmation, not for persistent errors. Persistent errors belong in inline error states. |
| Empty state | Explain what is missing and what the user can do next. |
| Not configured state | Explain which provider/module is not configured and direct Admins to the setup area. |

## Screen Density Guidance

| Screen | Density | Notes |
|---|---|---|
| Command Center | Medium | Balance metrics, chat, and operational next actions. |
| Claims Inbox | High | Dense table with filters, badges, preview drawer, and saved views. |
| Case Review | Medium-high | Evidence and action panels must be visible without excessive scrolling. |
| Document Review | High | Side-by-side document preview and extracted fields. |
| Admin Settings | Medium | Prioritize clarity, validation, and configuration confirmation. |

## Accessibility Requirements

- All interactive components must be reachable by keyboard.
- Focus states must be visible and not rely only on color.
- Semantic status colors must include text labels.
- Tables must support screen-reader-friendly headers.
- AI confidence indicators must be text-based as well as visual.
- Error messages must clearly identify the field or action that failed.

## Visual QA Checklist

- No clipped text at 100%, 125%, and 150% browser scaling.
- Left navigation does not overlap top bar or content.
- Copilot panel does not hide primary claim actions.
- Empty, loading, error, unauthorized, and Not configured states are implemented for every data region.
- Tables remain usable at laptop width.
- Modal and drawer content scrolls internally where required.
- Status badges remain readable on light background.


## Open Questions

| ID | Open Question | Owner | Impact if Unresolved |
|---|---|---|---|
| OQ-01 | Which insurer will be the first signed pilot customer and what exact internal processes will be included? | Product / Commercial | Final configuration, training, and pilot timelines may change. |
| OQ-02 | Which claims/core system will be integrated first, and will MVP require read-only lookup, export, or direct write-back? | Product / Engineering / Client IT | Impacts integration complexity and deployment schedule. |
| OQ-03 | Which Azure deployment path is selected first: customer-tenant Managed Application or Otic-hosted SaaS pilot? | Leadership / Engineering | Impacts tenancy, support access, IaC, billing, and security review. |
| OQ-04 | Which WhatsApp Business provider will be used for the first pilot? | Product / Integrations | Impacts webhook schemas, retry behavior, and channel onboarding. |
| OQ-05 | What client-specific SLAs, roles, branches, claim categories, and document checklists must be configured? | Client Operations | Impacts workflow rules and acceptance testing. |
| OQ-06 | What final regulatory, legal, and data residency requirements must be signed off for the first pilot? | Legal / Compliance | Impacts production readiness and contractual commitments. |
