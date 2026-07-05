---
Product: Insurance Intelligence Platform (IIP)
Document Name: Design System & UX Behavior Specification
Version: 1.2 - Ambient Intelligence Theme Update
Status: Draft - updated for Ambient Intelligence visual theme
Audience: Product, UX, Frontend Engineering, QA, Implementation Agents
Date: 2026-07-04
---

---

## July 2026 Update - Ambient Intelligence Visual Theme

This version supersedes the flat "Light Intelligence Workspace" palette below with an **Ambient Intelligence** theme, adopted to bring IIP Operations to full visual parity with the approved Lovable-designed reference (`docs/06-decisions/open-questions-tracker.md`, decision log). This is a **skin update only**:

- The chat-first Command Center architecture, block-rendering contract, evidence rail, and every governance/microcopy rule in this document remain unchanged.
- Surfaces move from flat white/bordered panels to a soft glass treatment (translucent, blurred, subtly elevated) over an ambient gradient page background.
- The primary/accent hues shift to a blue-to-violet gradient family; semantic status colors (success/warning/danger/info) keep their meaning and text-labels, only their exact hues are remapped.
- Token **names** already in `web/portal/app/globals.css` (`--color-*`, `--shadow-*`, `--radius-*`) are preserved and extended, not renamed, so no consuming component needs a class-name change for the base pass.

The token tables below have also been corrected to match the token names actually implemented in `web/portal/app/globals.css` / `tailwind.config.ts` (the previous `--iip-*` naming in this doc never matched the shipped code — this update closes that gap as well as updating the values).

> **Delivery note (2026-07-05).** This theme is delivered through the canonical `/app/*` route tree (Lovable-parity migration, ADR-2026-07-04). Two token layers currently coexist — `web/portal/app/globals.css` (RGB `--color-*`) and `web/portal/app/lovable.css` (HSL `--navy`/`--radius`/shadcn set); consolidating them into one is a tracked task (`docs/06-decisions/lovable-parity-migration-audit.md` §2.3). The Ambient Intelligence *values* here are authoritative regardless of which file holds them.

## July 2026 Update - Chat-First Command Center Direction

This version updates the document to align with the approved **chat-first Command Center** direction for **IIP Operations**.

Authoritative design decision:

> The Command Center is not a dashboard with chat added to it. The Command Center is a **chat-first operational command workspace** where metrics, claims, documents, evidence, approvals, actions, and reports appear as structured blocks inside the conversation.

The implementation must preserve the IIP production rules:

- no fake operational records, metrics, mailboxes, users, or provider states in deployed environments;
- all operational answers must be tenant-scoped, role-aware, source-grounded, and auditable;
- the AI may prepare, summarize, classify, recommend, route, and draft, but humans remain responsible for consequential insurance decisions;
- unavailable or unconfigured capabilities must show **`Not configured`** rather than simulated success;
- the approved **Ambient Intelligence** visual system (see July 2026 theme update above) remains in force.

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

Class names below use the tokens actually implemented in `web/portal/app/globals.css`/`tailwind.config.ts` (`bg`, `surface`, `line`, `ink`, `primary`, `ai`, `shadow-panel`, `rounded-xl`, etc.), plus the new glass/ambient utilities added for the Ambient Intelligence theme (`.glass`, `.glass-subtle`, `.ambient-bg`, `.hover-lift`).

| Pattern | Recommended classes |
|---|---|
| Command page shell | `flex min-h-0 flex-1 overflow-hidden ambient-bg` |
| Chat workspace | `flex min-h-0 flex-1 flex-col` |
| Chat scroll area | `custom-scrollbar min-h-0 flex-1 overflow-y-auto px-4 py-5 sm:px-6 lg:px-8` |
| Chat content width | `mx-auto w-full max-w-[var(--iip-command-chat-max-width)] space-y-4` |
| Assistant message card | `glass-subtle rounded-xl p-5 hover-lift` |
| User message bubble | `ml-auto max-w-[720px] rounded-2xl bg-primary-soft px-4 py-3 text-sm text-ink` |
| Metric pill strip | `grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6` |
| Metric pill | `glass-subtle rounded-lg px-3 py-3 hover-lift` |
| Embedded result block | `glass-subtle rounded-xl` |
| Sticky composer | `border-t border-line bg-surface/95 p-4 backdrop-blur` |
| Evidence rail | `hidden w-[var(--iip-evidence-rail-width)] shrink-0 border-l border-line glass xl:block` |

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

## Confirmed Core Design Tokens — Ambient Intelligence Theme

These are the real token names in `web/portal/app/globals.css`, with values remapped to the Lovable-derived Ambient Intelligence palette. Token **names** are unchanged from the prior flat theme; only hues move to the blue→violet gradient family. Values are RGB triplets (matching the file's `R G B` convention, consumed via `rgb(var(--x) / <alpha-value>)`).

```css
:root {
  /* App shell */
  --color-bg: 248 250 252;              /* #F8FAFC */
  --color-shell: 249 250 251;           /* #F9FAFB */

  /* Primary surfaces (glass-treated, see Glass & Gradient Surface Tokens below) */
  --color-surface: 255 255 255;         /* #FFFFFF */
  --color-surface-soft: 248 250 252;    /* #F8FAFC */
  --color-surface-muted: 241 245 249;   /* #F1F5F9 */
  --color-surface-raised: 255 255 255;  /* #FFFFFF */

  /* AI-specific surfaces */
  --color-surface-ai: 245 243 255;      /* #F5F3FF - soft violet AI surface */
  --color-surface-ai-strong: 237 233 254; /* #EDE9FE */
  --color-surface-ai-border: 221 214 254; /* #DDD6FE */

  /* Typography */
  --color-ink: 11 15 59;                /* #0B0F3B - Ambient Intelligence base ink (was #0F172A) */
  --color-ink-soft: 30 41 59;           /* #1E293B */
  --color-muted: 100 116 139;           /* #64748B */
  --color-muted-soft: 148 163 184;      /* #94A3B8 */

  /* Brand / Accents — Ambient Intelligence gradient family */
  --color-primary: 37 99 255;           /* #2563FF - info blue (was #1E40AF navy/azure) */
  --color-primary-hover: 29 78 216;     /* #1D4ED8 */
  --color-primary-soft: 239 246 255;    /* #EFF6FF */
  --color-primary-border: 191 219 254;  /* #BFDBFE */

  --color-ai: 124 77 255;               /* #7C4DFF - violet accent (was #6D28D9) */
  --color-ai-hover: 140 107 255;        /* #8C6BFF */
  --color-ai-soft: 245 243 255;         /* #F5F3FF */
  --color-ai-border: 221 214 254;       /* #DDD6FE */

  /* Semantic status colors — hues unchanged, carried forward as-is */
  --color-success: 5 150 105;           /* #059669 */
  --color-warning: 217 119 6;           /* #D97706 */
  --color-danger: 220 38 38;            /* #DC2626 */
  --color-info: 37 99 235;              /* #2563EB */
}
```

## Glass & Gradient Surface Tokens

New tokens/utility classes added for the Ambient Intelligence theme, defined in `web/portal/app/globals.css` and `tailwind.config.ts`. Additive — existing tokens above are untouched by these.

| Token / Class | Value | Use |
|---|---|---|
| `--gradient-primary` | `linear-gradient(135deg, #2563FF 0%, #7C4DFF 100%)` | Primary CTAs, active nav indicators, key accents. |
| `--gradient-ambient` | Radial multi-stop wash (indigo/violet/mint tints) | Page-level background behind the app shell. |
| `--shadow-glass` | `0 8px 32px rgba(31,38,135,0.10)` | Default glass panel elevation. |
| `--shadow-l1`…`--shadow-l4` | Depth scale, `rgba(0,0,0,...)` increasing | General-purpose elevation scale for non-glass cards. |
| `.glass` | 72% white, 24px blur, 1px translucent border, `--shadow-glass`, `rounded-2xl` | Primary panel/card surface. |
| `.glass-subtle` | 55% white, 18px blur | Lower-emphasis embedded blocks (chat message cards, metric pills). |
| `.glass-elevated` | 85% white, 30px blur, stronger shadow | Modals, drawers, popovers. |
| `.ambient-bg` | `background-color: #FBFAFF` + `--gradient-ambient` | App shell background. |
| `.gradient-text` | `--gradient-primary` clipped to text | Rare emphasis headings only — not body text. |
| `.hover-lift` | `translateY(-1px)` + shadow on hover, 180ms | Default micro-interaction for interactive cards/rows. |

## Recommended Implementation Tokens

These tokens should be implemented as CSS variables or Tailwind theme extensions. Values not already confirmed should be treated as implementation defaults and may be tuned after visual QA.

| Token | Value | Use |
|---|---:|---|
| `--radius-sm` | `8px` | Tags, small buttons, small inputs. |
| `--radius-md` | `12px` | Cards, drawers, inputs, compact panels. |
| `--radius-xl` | `20px` | Main panels, modals, large cards (glass surfaces default here). |
| `--shadow-panel` | `0 8px 24px rgba(15, 23, 42, 0.06)` | Normal cards and panels not using glass. |
| `--shadow-popover` | `0 18px 50px rgba(15, 23, 42, 0.16)` | Modals, popovers, floating Copilot elements. |
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
| App shell | `min-h-screen ambient-bg text-ink` |
| Main card | `glass rounded-xl hover-lift` |
| Dense table row | `h-12 border-b border-line hover:bg-surface-soft` |
| Primary button | `rounded-md bg-gradient-primary px-4 py-2 text-inverse hover:opacity-90` |
| Accent action | `rounded-md bg-ai px-4 py-2 text-inverse hover:bg-ai-hover` |
| Muted text | `text-sm text-muted` |
| Danger badge | `rounded-pill bg-danger-soft px-2 py-0.5 text-xs text-danger-text` |
| Warning badge | `rounded-pill bg-warning-soft px-2 py-0.5 text-xs text-warning-text` |
| Info badge | `rounded-pill bg-info-soft px-2 py-0.5 text-xs text-info-text` |

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
