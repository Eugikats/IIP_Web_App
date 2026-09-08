# Bevaya — Design Language & Product Reference

*Compiled from a hands-on walkthrough of the Bevaya demo tour (tour.demo.bevaya.ai) on 8 September 2026: the Assistant/conversation screen, Organizations & Workspaces, workspace Members & Roles, Profile, Notifications, Security, Items, AI Agents, and Insights. Colors and metrics below were sampled directly from the live, rendered DOM (computed styles), not estimated from screenshots, so they should be treated as accurate to the pixel.*

This document exists so a different product can borrow Bevaya's visual and interaction language convincingly. It covers the full system: color, type, spacing, components, motion, and how the screens function together. A companion file, `bevaya-ui-replica.html`, is a working single-file HTML/CSS/JS reproduction of nine of these screens built from the same tokens.

---

## 1. What Bevaya Is

Bevaya is an enterprise SaaS product for insurance carriers, positioned around the tagline **"Automate Claims. Underwriting. Policy Servicing."** It's an AI-agent operations console: a company (an "Organization," e.g. "Acme Ins.") sets up one **workspace per line of business** (Auto Liability, Commercial Property, Workers Compensation, General Liability, Marine & Cargo, Cyber & Tech E&O, etc.), and inside each workspace they configure **AI Agents** (e.g. "FNOL Triage," "Subrogation Recovery," "Litigation Review") that read inbound documents and correspondence, classify and extract data from them, and either resolve them automatically or route them to a human as a **work item** requiring review.

The product's core loop, screen by screen:

- **Assistant** — a conversational, ChatGPT-style interface scoped to the user's own operational data ("Items processed yesterday?", "Which flows failed overnight?"). It's the front door and doubles as a natural-language reporting tool.
- **Items** (the work queue) — every claim, submission, endorsement, or correspondence bundle the AI has touched, with status (Requires Review / In Progress / Completed / Exceptions), filterable by which AI Agent produced it. Clicking an item opens a detail view with an AI-written narrative summary, a confidence score, extracted fields, and Save/Submit actions for a human reviewer.
- **AI Agents** — the automations themselves, shown as cards with a live "% by agent," item counts, and a "requires review" count — essentially a fleet-management view of the bots.
- **Flows** — (present as a nav icon; not fully explored) presumably the builder/orchestration layer behind each Agent.
- **Review Queue** — a focused subset of Items awaiting a human decision.
- **Insights** — an analytics dashboard: items processed, auto-resolution rate, average handle time, exceptions, and a throughput bar chart.
- **Organizations / Workspaces** (Settings → Workspaces) — administration of the multi-tenant structure: every line of business as a workspace, each with its own member roster, AI agent roster, and status (Active / In setup).
- **Settings** — a conventional two-tier settings shell: **Personal** (Profile, Notifications, Security, API tokens, Appearance) and **Workspace/Organization** (Members & roles, API key audit, Workspace variables, AI Agents, Workspaces, Audit log, SSO, Billing — several marked "Soon").

The org/workspace switcher (top-left) reveals that a single user's login can span **multiple organizations** (the demo shows "Acme Insurance Group" and "Vertex Mutual Insurance" as separate tenants with separate workspace lists) — this is a genuine multi-tenant B2B structure, not just a single company's internal tool.

---

## 2. Overall Layout & Structure

Bevaya uses a **fixed three-tier shell** on every screen:

1. **64px icon rail** (far left, always visible, white background) — the primary app-level navigation. Top to bottom: a small brand mark, a 40×40 rounded-square "org switcher" button (shows the current org's initials, e.g. "AI" for Acme Insurance), then a vertical stack of ~7 nav icons (Items, Organizations, Assistant, Items again/Review, AI Agents, Flows, Review Queue, Insights), then — pinned to the bottom — a settings gear, a "create" plus-button, and the user's circular avatar (with a small green online-status dot).
2. **A contextual side panel** (256–320px), present on most screens: conversation history on the Assistant screen, a Settings navigation tree on Settings screens, a workspace's sub-navigation (Members & roles / API key audit / Workspace variables / AI Agents) when inside a workspace. This panel is white with a 1px right border, matching the rail.
3. **The main content column** — background is a very light warm-gray (`#F5F6F8`), content constrained to roughly 960–1040px and left-aligned (not centered), with generous 28–32px outer padding.

There is no top bar in the traditional sense; page titles, breadcrumbs, and primary actions live at the top of the main column itself, not in a fixed header — except a small bell (notifications) icon that floats top-right on some data screens.

**Breadcrumbs** appear consistently at the very top of settings/admin screens in muted gray with `›` separators, the current page in bold dark text: `Settings › Acme Ins. › Workspaces`.

---

## 3. Color System

Bevaya's palette is disciplined: one saturated brand purple, a neutral gray scale for text and structure, and three semantic colors (red/orange/green) used almost exclusively as small status dots and stat numbers — never as large color blocks. The overall impression is "quiet SaaS with one confident accent," in the same family as Linear, Vercel, or Stripe Dashboard.

### Brand / Primary

| Token | Hex | RGB | Usage |
|---|---|---|---|
| `purple-600` (primary) | `#5B2AE8` | 91, 42, 232 | Primary buttons, active nav state, links (item IDs like `CI-8801`), focus rings, the org-switcher square, user chat bubbles |
| `purple-500` (accent/active-text) | `#7C57EE` | 124, 87, 238 | Active filter-pill text (e.g. the selected "All AI Agents" tab), secondary emphasis |
| `purple-50` (tint) | `#F1ECFE` / `#F5F3FF` | ~241,236,254 | Selected nav-item background, selected settings-link background, AI-agent tag chip background, AI message icon background |
| Orb gradient | — | radial-gradient(circle at 32% 28%, `#BCACF9`, `#5B2AE8` 55%, `#3C1AA3`) | The glowing sphere on the Assistant's empty state ("Good morning, Hazal,") |

### Neutrals (text & structure)

| Token | Hex | RGB | Usage |
|---|---|---|---|
| `gray-900` (text primary) | `#101828` | 16, 24, 40 | Headings, primary body text, big stat numbers |
| `gray-600` (text secondary) | `#525C6E` | 82, 92, 110 | Labels, card captions, muted descriptions |
| `gray-400` (text faint) | `#98A2B3` | 152, 162, 179 | Placeholder text, inactive filter counts, timestamps, disabled-field text |
| `border-subtle` | `#ECEEF2` | 236, 238, 242 | Card borders, rail/panel dividers (all ~0.74px — see §7) |
| `border-input` | `#DDE1E8` | 221, 225, 232 | Input/button borders |
| `border-pill` | `#E5E7EB` | 229, 231, 235 | Status-pill borders |
| `bg-page` | `#F5F6F8` | 245, 246, 248 | Main content background |
| `bg-surface` | `#FFFFFF` | 255, 255, 255 | Cards, rail, panels, inputs |

### Semantic (status)

Each has a small 6px **dot** color and a darker **text/number** color used for large KPI figures — they are never used as a filled background on their own except inside the light "Enabled" status pill.

| Status | Dot (small indicator) | Text / big-number color | Tint background (pills only) |
|---|---|---|---|
| Error / Requires Review | `#F04438` | `#D92D20` | — |
| Warning / In Review | `#F79009` | `#B54708` (inferred, standard amber-700 pairing) | — |
| Success / Active / Completed | `#17B26A` | `#067647` | `#ECFDF3` (used behind "Enabled" pill) |
| Info | — | `#175CD3` | — |

**Rule of thumb:** color is rationed. A screen is 90% white/gray/black; the purple appears once or twice as the clear call to action, and red/orange/green appear only as small dots or numerals to draw the eye to what needs attention. Nothing is ever a solid red or orange card/button — severity is communicated through a tiny dot plus a bold number, which reads as calm even when the number is bad news.

---

## 4. Typography

- **Typeface:** [Inter](https://fonts.google.com/specimen/Inter) throughout — `font-family: Inter, ui-sans-serif, system-ui, -apple-system, sans-serif;`. No serif or monospace appears anywhere in the explored screens (not even for IDs like `SUB-4471`, which are set in Inter, just colored and semi-bold).
- **Base size:** 14px body text; 16px is the browser/`<html>` default but is overridden to 14px at the `<body>` level.
- **Scale observed:**

| Role | Size | Weight | Extra | Example |
|---|---|---|---|---|
| Page H1 | 23px | 700 | letter-spacing ‑0.41px, line-height ~1.25 | "Workspaces", "Profile", "Security" |
| Card title / section head | 15–17px | 700–800 | | "Photo & identity", "New workspace" modal title |
| Stat number (KPI) | 30px | 700 | color varies by semantic status | "10", "1,499", "86%" |
| Body / form values | 14px | 400 | | input text, paragraph copy |
| Secondary/meta text | 13–13.5px | 400–600 | color `gray-600` | "3 AI agents · 8 members" |
| Small label / eyebrow | 11–12px | 700 | uppercase, letter-spacing ~0.05em, color `gray-400` or purple | "ORGANIZATION · ACME INS.", "PERSONAL", table column headers |
| Pill/badge text | 12px | 500 | | "Requires Review", "Active" |
| Micro/footnote | 11.5–12.5px | 400 | color `gray-400` | "Bevaya AI Assistant can make mistakes..." |

Headings use a small negative letter-spacing (tight, confident); labels and eyebrows use *positive* letter-spacing with uppercase — a common but effective contrast that makes big numbers feel large and small labels feel structured/technical.

---

## 5. Spacing, Radius & Elevation

- **Base spacing unit:** 4px, but the dominant rhythm is built on **8px** — gaps of 6/8/10/12/14/16/20px recur constantly. Card interior padding is consistently **16px vertical / 20px horizontal**. Section stacks (card-to-card) use **16px** gaps. Page outer padding is **28–32px**.
- **Border radius scale** (small, deliberate set — nothing improvised):
  - `4px` — tight inner corners (e.g. the "sharp corner" side of a chat bubble, pointing toward its sender)
  - `8px` — buttons, inputs, small icon tiles, the icon-rail's org square corners on the smaller elements
  - `9–10px` — AI icon chips, nav-icon squares, workspace-row icon tile
  - `12px` — the standard **card** radius (this is the single most-used radius in the system)
  - `14–16px` — modals, the message composer box, chat bubbles
  - `9999px` (fully round) — all pills/badges, toggle switches, avatars, the org-switcher's status dot, filter tabs
- **Borders, not shadows, define structure.** Cards use a hairline `~0.74px solid #ECEEF2` border and **no box-shadow** (`box-shadow: none` was the measured value even on cards that look "raised" in a screenshot — the separation reads entirely through the border + the page's light-gray background behind white cards). The one clear elevation exception is the **org-switcher popover** and the **New Workspace modal**, which do use a soft, large-radius drop shadow (`0 20px 40px -12px rgba(16,24,40,.22)` popover; a slightly heavier one for the modal) to lift them above page content — shadow is reserved for genuinely floating/overlay UI, never for static cards.
- **Sub-pixel borders** (`0.74px` rather than `1px`) appear throughout, which is a devicePixelRatio artifact of a `1px` border rendered at ~1.35x zoom in the captured environment — for a from-scratch build, just use a clean `1px` solid border; the visual intent is "hairline," not a specific fractional value.

---

## 6. Components

### Buttons
- **Primary:** solid `#5B2AE8` fill, white text, `700` weight, `8px` radius, `13px` font-size, roughly `9px 16px` padding, subtle purple-tinted shadow (`0 1px 2px rgba(91,42,232,.25)`). Hover darkens slightly (~`#4C22C9`).
- **Secondary:** white fill, `1px` `#DDE1E8` border, dark text, `600` weight — used for "Manage," "Cancel," "Upload new photo."
- **Ghost/text:** no border or fill, muted-gray text, used for low-emphasis actions like "Cancel" next to a primary "Save changes."
- **Disabled state:** primary buttons at rest with no pending change (e.g. "Save changes" before any edit) render at roughly 60% opacity of the solid purple — a lighter lavender rather than gray, keeping the button legible as "will be purple" rather than looking broken.

### Pills / Status badges
A pill is an `inline-flex` element: `border-radius: 9999px`, `1px solid #E5E7EB` border, `3px 8px` padding, `12px/500` text in the *default dark text color* (`#101828`) — **the text itself is not colored**; only a small `6px` circular dot to the left of the label carries the status color (red/orange/green). This is a deliberate, consistent pattern across the whole product — status is color-coded once, minimally, via the dot, not by tinting the whole pill or the text.

### Cards
White surface, `12px` radius, `1px` `#ECEEF2` border, no shadow, `16px 20px` internal padding. A card may have an internal header row (bold 15px title) separated from its body by a `1px` divider with extra bottom padding/margin. Stat cards are the same card treatment, just with a small muted label on top and one large (30px/700) number below.

### Tables
Minimal-chrome tables: no zebra striping, no cell borders except a `1px` `#ECEEF2` rule under every row (including the header). Header cells are `11.5px/700` uppercase muted-gray with letter-spacing. Row height is generous (~54–66px) thanks to `12–16px` vertical cell padding, giving avatars room to breathe.

### Avatars
Circular, solid-color background (each person gets a distinct saturated but muted color — navy, blue, terracotta, violet, teal, forest-green — seemingly hashed from their name), white initials at `11–12px/700`. The current user's own avatar (bottom of the icon rail) additionally carries a small green "online" dot with a white ring, bottom-right.

### Toggle switches
Track `36×20px`, fully rounded, off-state light gray (`#E4E7EC`) with a white knob; on-state track turns solid purple (`#5B2AE8`) and the knob slides right. No labels inside the switch — the row's leading text is the label. Used pervasively on the Notifications screen in an Email/In-app matrix layout.

### Inputs
White fill, `1px` `#DDE1E8` border, `8px` radius, `14px` text, `~9px 12px` padding (36px left-padding when a leading search icon is present). Focus state adds a purple border plus a soft `3px` purple glow (`box-shadow: 0 0 0 3px rgba(91,42,232,.12)`) — no heavy outline. Disabled/locked fields (e.g. an SSO-managed email address) get a faint gray fill and gray text, paired with a small lock icon and explanatory caption below ("Managed by your organization · contact an admin to change").

### Filter tabs / segmented pills
A horizontal row of rounded, pill-shaped tabs (not underlined tabs) — e.g. "All AI Agents 17 / FNOL Triage 6 / ...". The active tab gets a light purple tint background (`#F5F3FF`) with a slightly darker purple border and purple-500 text (including its count); inactive tabs are plain white with a gray border and gray-400 counts.

### Modals
Centered overlay, dark scrim (`rgba(16,24,40,.45)`), white modal panel with `16px` radius and a strong soft shadow. Structure is strict: header (title + one-line description + a small "✕" close button, top-right), a body with vertically-stacked form fields (`16px` gaps), and a footer row (right-aligned) with a ghost "Cancel" and a solid primary confirm action, separated from the body by a hairline divider. *(Note: the live demo's own "New workspace" button was inert/non-functional in the tour — the modal shown in the accompanying HTML replica is a faithful, from-scratch reconstruction built to match every other modal-like surface in the product, specifically the org-switcher popover's shadow/radius/type treatment.)*

### Popover (org/workspace switcher)
A `320px`-wide floating panel anchored near the icon that opened it, white, `14px` radius, soft shadow, internally sectioned: a "CURRENT" block (org square + name + current workspace), a search input, a "Recents" group, then one expandable group per organization the user belongs to (org name + workspace count on the right, then indented workspace rows each showing an AI-agent count or a purple checkmark if selected). This is effectively a command-palette-style switcher, not a plain dropdown.

### Chat / conversation surface
- **Empty state:** centered vertically, a glowing purple orb (64px radial-gradient sphere, described above) sits above a large friendly greeting ("Good morning, Hazal,") and a muted subhead ("How can I help with your work today?"), followed by 2–3 horizontally-arranged **suggestion chips** (rounded rectangle, icon + short prompt text, e.g. "🛡 Items processed yesterday?").
- **User messages:** right-aligned, solid purple pill/bubble, white bold text, tightly rounded except the corner nearest the sender (a `16px 16px 4px 16px` radius — the "tail" corner is much tighter).
- **AI messages:** left-aligned, preceded by a small 26px rounded-square purple-tinted icon (a sparkle/agent glyph), white card bubble with a `1px` border, generous `16px 20px` padding, `4px 16px 16px 16px` radius (tail corner top-left this time), and body copy that supports bold and bullet lists for structured answers ("• 96% completed straight-through...").
- **Composer:** a single rounded (`14px`) bordered box pinned above the very bottom, containing an auto-growing text field, a row of small utility icons (attach, emoji) bottom-left, and a circular send button bottom-right that is a *pale lavender disc* while the field is empty and turns *solid purple* the instant text is entered — a nice, cheap "is this ready to send" affordance. A one-line disclaimer ("Bevaya AI Assistant can make mistakes...") sits centered beneath the whole composer.
- **Conversation history sidebar:** a "+ New Conversation" outlined button up top, then a flat list of past conversation titles (auto-titled from the first question asked), the active one highlighted with a light-purple background, purple text, and a `3px` purple left border-accent.

### Charts
Only one chart type observed: a simple **vertical bar chart** (throughput by day). Bars use a top-to-bottom **purple gradient** (`linear-gradient(180deg, #8D6BF1, #5B2AE8)`), `8px` top corner radius tapering to a near-square bottom, generous gaps between bars (roughly bar-width ≈ gap), single-letter day labels (M T W T F S S) in muted gray below each bar. No gridlines, no axis, no legend — the chart is illustrative/ambient rather than analytically dense, consistent with the rest of the product's "confident, uncluttered" tone.

### Item detail — AI summary card
Worth calling out separately because it's the most distinctive surface in the product: opening a work item shows a full-width **dark maroon/brick-red card** (a deep desaturated red, roughly `#7A1F1F`–`#8B2A1F` range, not the same red as the status dots) containing a small warning-triangle icon + all-caps eyebrow ("INDEXING COMPLETE — 1 LOW-CONFIDENCE FLAG"), a large bold headline, and 2–4 sentences of plain-English narrative explaining exactly what the AI did and why something needs a human look. A large ghost watermark icon (the same warning triangle, oversized and low-opacity) sits bottom-right of the card as a decorative flourish. Below it, a "Key Insights" accordion lists individually expandable findings, each with a small leading status icon (check/document/etc.) in a tinted rounded-square chip. This card is the one place the product breaks its "color is rationed to tiny dots" rule — and it's used deliberately, only for the single most important AI-generated narrative on the page, which makes it land as a genuine attention signal rather than visual noise.

---

## 7. Iconography

- **Style:** outline/stroke icons only (no filled icons in primary UI), consistent ~1.8–2px stroke weight, rounded line caps and joins — reads as a standard Feather/Lucide-style icon set.
- **Size:** small and consistent — 14–19px for inline/rail icons, ~26–34px containers for "icon chips" (AI message avatar, workspace-row icon, session-device icon), which themselves use a light purple or light gray tinted rounded-square background.
- **Never multi-color** — icons inherit `currentColor`, colored only via their container's text-color (gray by default, purple when active/selected).

---

## 8. Motion & Interaction

Nothing exotic was observed — the product favors instant, low-motion state changes over flashy transitions:
- Hover states are simple background-tint swaps (transparent → `bg-page` gray, or → light purple tint for already-active items).
- The composer's send button flips from muted to solid purple instantly on input, no animation needed to communicate readiness.
- Popovers/modals appear to use a short fade/scale-in (standard for this class of component) rather than a slide.
- Toggle switches use a short (~150ms) ease on the knob's position and track color.

---

## 9. Voice & Content Patterns

- **Friendly but efficient copy.** Greetings are warm and personal ("Good morning, Hazal,") but every other string is compact and information-dense.
- **Numbers do the talking.** Card labels are short nouns ("Requires Review," "Auto-resolution rate"); the actual information is the big bold number beside/below it.
- **AI narrates itself in first person, plainly.** The assistant and the item-summary cards both explain *what happened and why* in short, concrete sentences ("The 4-message email thread was treated as one document... Five classifications are clean (≥0.92)"), citing specific confidence numbers and counts rather than vague reassurance. This is a meaningful design decision worth carrying into a new product: the AI shows its work.
- **Status language is neutral, not alarmist**, even for the red "Requires Review" state — it reads as a routing decision, not an error.
- **"Soon" tags** on unshipped settings (Flow defaults, Audit log, SSO, Billing) are handled with a plain small gray pill rather than hiding the nav item — signals roadmap transparency.

---

## 10. Functional Summary (for reference)

| Area | What it does |
|---|---|
| **Assistant** | Natural-language Q&A over the org's live operational data; suggested prompts; persistent conversation history; answers are structured (bold figures + bullet lists) rather than free-form prose. |
| **Items** | The universal work queue across every AI Agent in the current workspace. Stat tiles (Requires Review / In Progress / Completed / Exceptions), filter pills per-agent, searchable/sortable table, exportable, configurable columns. Row click opens a full item detail with an AI-narrated summary, confidence score, expandable "Key Insights," a Review tab (presumably the extracted-field editor), a reviewer-assignment dropdown, and Save/Submit actions. |
| **AI Agents** | Fleet view of the automations configured in this workspace — name, active/inactive state, one-line purpose, items processed, % handled autonomously, and current review backlog per agent. "+ New AI agent" to configure another. |
| **Flows** | (Icon present; presumably the no-code/low-code builder behind each Agent — not explored in this pass.) |
| **Review Queue** | Focused, filtered view of only the items awaiting human action (subset of Items). |
| **Insights** | Workspace-level analytics: 30-day volume, auto-resolution rate, average handle time, exceptions, and a 7-day throughput bar chart. A "Focus" mode button suggests a fullscreen/presentation view. |
| **Organizations / Workspaces** (admin) | Every line-of-business workspace in the current organization, its status (Active/Setup), agent and member counts, with a "New workspace" creation flow and per-workspace "Manage" drill-in. |
| **Workspace → Members & Roles** | Per-workspace member roster with role assignment (Workspace Admin, Super Admin, Flow Builder, Reviewer), per-agent access grants (small folder icons per AI agent a member can touch), and account status. A "Roles" tab (not explored) presumably defines the permission sets themselves. |
| **Settings → Profile** | Personal identity: photo, name, job title, an org-locked email field, and timezone. |
| **Settings → Notifications** | An event × channel (Email / In-app) toggle matrix for five personal notification types, applying across all workspaces. |
| **Settings → Security** | Password change, two-factor authentication status/management, and a live list of active sessions by device with per-session revoke (except the current device). |
| **Settings → API tokens / Appearance / Workspace variables / API key audit** | Present in navigation; lower-depth utility screens for personal API access, theme, environment variables, and key-usage auditing. |
| **Org switcher** | A command-palette-style popover for jumping between organizations and their workspaces, with a "Recents" section and per-org workspace lists showing live agent counts. |

---

## 11. Quick-Reference Token Sheet

```css
:root {
  /* Brand */
  --purple-600: #5B2AE8;   /* primary actions, links, active states */
  --purple-500: #7C57EE;   /* active filter/tab text */
  --purple-50:  #F1ECFE;   /* selected-state tint backgrounds */

  /* Neutrals */
  --gray-900: #101828;     /* primary text / headings */
  --gray-600: #525C6E;     /* secondary text */
  --gray-400: #98A2B3;     /* faint / disabled text */
  --border-subtle: #ECEEF2;
  --border-input:  #DDE1E8;
  --border-pill:   #E5E7EB;
  --bg-page:    #F5F6F8;
  --bg-surface: #FFFFFF;

  /* Semantic */
  --red-dot:    #F04438;  --red-text:    #D92D20;
  --orange-dot: #F79009;  --orange-text: #B54708;
  --green-dot:  #17B26A;  --green-text:  #067647; --green-tint: #ECFDF3;
  --blue-text:  #175CD3;

  /* Type */
  --font: 'Inter', ui-sans-serif, system-ui, sans-serif;
  --h1-size: 23px; --h1-weight: 700; --h1-tracking: -0.41px;
  --stat-size: 30px; --stat-weight: 700;
  --body-size: 14px;
  --label-size: 12px; --label-weight: 700; --label-tracking: 0.05em;

  /* Radius */
  --radius-btn:  8px;
  --radius-card: 12px;
  --radius-modal: 16px;
  --radius-pill: 9999px;

  /* Elevation */
  --shadow-popover: 0 20px 40px -12px rgba(16,24,40,.22);
  --shadow-btn-primary: 0 1px 2px rgba(91,42,232,.25);
}
```

---

*Companion file: `bevaya-ui-replica.html` — a self-contained, dependency-light HTML/CSS/JS reproduction of the Assistant, Organizations/Workspaces, New Workspace (modal), Workspace Members, Items, AI Agents, Insights, Profile, Notifications, and Security screens, built directly from these tokens. Open it in any browser; every nav icon, settings link, toggle, tab, and the org switcher / new-workspace modal are functional (backed by small in-memory JS state, not a real backend) so it can be clicked through exactly like the source product.*
