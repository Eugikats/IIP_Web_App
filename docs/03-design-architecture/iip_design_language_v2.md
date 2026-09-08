# IIP Design Language v2 — Tokens, Geometry, and Component Contracts

| Field | Value |
|---|---|
| Version | 2.0 |
| Date | 2026-09-08 |
| Status | **Approved visual direction.** Supersedes the token, palette, geometry, and component-treatment content of `iip_visual_design_system_and_tokens.md` and closes the open direction selection in `iip_visual_direction_exploration.md` / `iip_visual_direction_review.md`. |
| Applies to | `web/portal` — the authenticated staff portal (`/app/*`), claimant access surfaces (`/access/*`), and sign-in. |
| Companion documents | `iip_ux_cognitive_load_and_navigation_model.md` (navigation, chat-first landing, attention policy), `iip_workflow_legibility_contract.md` (how the claim lifecycle is rendered), `iip_ui_craft_guardrails.md` (anti-generic guardrails and the emoji ban) |
| Visual reference | `reference/bevaya-design-language.md` and `reference/bevaya-ui-replica.html` |

## 0. How to use this document

This document is written so that an implementing agent needs no further design input. Every value is literal. Where a value is not stated here, it does not exist — do not invent one; add it to this document first with a rationale, then implement it.

Precedence when this document meets another:

1. `AGENTS.md` invariants (security, tenant isolation, human authority, no-simulation) always win. A visual rule never justifies weakening them.
2. `iip_workflow_legibility_contract.md` wins on *what state must be visible*.
3. `iip_ux_cognitive_load_and_navigation_model.md` wins on *where something lives and when it interrupts*.
4. This document wins on *how it looks*.
5. `iip_visual_design_system_and_tokens.md` retains authority only for accessibility method, non-color encoding requirements, chart data-quality states, and the design-token pipeline format. Its palette, radius, elevation, card policy, and shell dimensions are superseded here.

---

## 1. The decision, in one paragraph

IIP Operations adopts a **calm, border-led, light-first operations console** with one saturated violet accent, an Inter type system, hairline borders instead of shadows, and colour rationed to small status indicators and single figures. The reference implementation for this language is Bevaya (see `reference/`). We adopt its visual grammar — 64px icon rail, contextual panel, 12px card, dot-plus-label status pill, violet primary, no shadow on static surfaces — because it solves the same problem we have: dense insurance operations work that must stay legible for eight hours without fatigue. We do **not** adopt its product scope. Anything in the reference that serves Bevaya's business and not ours (organisation creation, workspace-per-line-of-business administration, billing, its agent-marketplace framing) is out.

The character we are aiming at: **a precise instrument, not a product tour.** A senior claims officer should be able to open it at 08:00, see what needs them, and act — without a single decorative element competing for that attention.

## 2. What we take, what we change, what we reject

| From the reference | Decision | Reason |
|---|---|---|
| 64px icon rail + contextual panel + content column | **Take** | Keeps global navigation to one glanceable column and frees horizontal space for evidence. |
| `#5B2AE8` violet primary, rationed | **Take** | One confident accent; every other pixel stays neutral. |
| Status = neutral pill + 6px coloured dot + text label | **Take** | Colour never carries meaning alone, which is also our accessibility requirement. |
| Hairline `1px` borders, `box-shadow: none` on static cards | **Take** | Shadow is reserved for genuinely floating surfaces. |
| Inter, 14px body, 23px H1, 30px stat | **Take** | Compact hierarchy that survives dense tables. |
| Chat-first Assistant as the front door | **Take and extend** | Ours must hand off into real work, not merely answer. See the navigation model §6–7. |
| `#98A2B3` used for meaningful text | **Change** | 2.58:1 on white. Fails WCAG 2.2 AA. See §4.3. |
| Deep maroon AI narrative card | **Change** | Kept as a pattern, re-toned and re-scoped. See §8.19. |
| Emoji in Appearance settings (`sun/moon/monitor` glyphs) and suggestion chips | **Reject** | See `iip_ui_craft_guardrails.md` §3. Emojis are banned product-wide. |
| Organisations, workspace-per-LOB, "Create organisation", Billing, "Soon" roadmap tags | **Reject** | IIP is tenant-per-insurer and provisioned by us. Nothing in the product may advertise unshipped features. |
| Violet gradient on bars and orb | **Restrict** | Gradients are permitted in exactly two places. See §4.5. |

---

## 3. Layout and shell geometry

All values are exact. Do not round, scale, or "improve" them.

| Region | Value |
|---|---|
| Icon rail width | `64px`, fixed, `flex: 0 0 64px`, background `--color-surface`, `border-right: 1px solid --color-border-subtle` |
| Rail vertical padding | `14px 0` |
| Rail brand mark | `32px × 32px`, radius `8px`, `margin-bottom: 18px` |
| Rail tenant mark | `40px × 40px`, radius `10px`, `margin-bottom: 16px` |
| Rail nav button | `40px × 40px`, radius `8px`, `4px` gap between buttons |
| Rail icon | `19px`, stroke `1.8` |
| Rail avatar | `34px` circle, `9px` presence dot with `2px` white ring |
| Contextual panel width | `256px` default; `288px` where the panel holds a document or evidence list; never above `320px` |
| Panel treatment | background `--color-surface`, `border-right: 1px solid --color-border-subtle`, no shadow |
| Content column background | `--color-bg-page` |
| Content max width | `960px` standard; `1040px` for table-led and analytics surfaces |
| Content padding | `28px 32px 60px` |
| Content alignment | Left-aligned inside its max-width. Never centred. |
| Card-to-card gap | `16px` |
| Card padding | `16px 20px` |
| Field stack gap inside a card | `16px` |

There is **no fixed top bar.** Page title, breadcrumb, and the primary action live at the top of the content column. The only persistently floating chrome is the notification bell (top-right of the content column) and the environment banner when a non-production runtime is active.

### 3.1 Breadcrumbs

Rendered at the very top of the content column on every surface below the first level. `13px`, `--color-text-muted`, ` › ` separators with `6px` gaps, current page in `--color-text-primary` at weight `600`. Format: `Section › Object › Current page`. Never more than four levels; collapse the middle with an ellipsis if it would exceed four.

### 3.2 Page header block

Order, top to bottom, always: breadcrumb → eyebrow (optional) → `h1` + optional inline count → one-line description → primary action (right-aligned, on the same row as the `h1`). One primary action per page. Additional actions are secondary or live in a menu.

---

## 4. Colour

### 4.1 Token set

Emit these into `web/portal/app/globals.css` as space-separated RGB triplets so the existing `rgb(var(--token) / <alpha-value>)` Tailwind pipeline in `web/portal/tailwind.config.ts` keeps working unchanged.

```css
:root {
  /* ---- Brand ---- */
  --color-primary:            91 42 232;    /* #5B2AE8  primary action, link, active nav, focus */
  --color-primary-hover:      76 34 201;    /* #4C22C9 */
  --color-primary-active:     60 26 163;    /* #3C1AA3 */
  --color-primary-accent:     124 87 238;   /* #7C57EE  active tab/filter text and counts */
  --color-primary-soft:       241 236 254;  /* #F1ECFE  selected nav, agent chip, AI avatar tile */
  --color-primary-soft-alt:   245 243 255;  /* #F5F3FF  selected settings row, active filter tab */
  --color-primary-border:     220 209 251;  /* #DCD1FB  border of an active filter tab */

  /* ---- Neutrals ---- */
  --color-text-primary:       16 24 40;     /* #101828  headings, body, stat numbers */
  --color-text-secondary:     82 92 110;    /* #525C6E  labels, captions, descriptions */
  --color-text-muted:         107 114 128;  /* #6B7280  ANY meaningful small text (see 4.3) */
  --color-text-faint:         152 162 179;  /* #98A2B3  DECORATIVE ONLY (see 4.3) */
  --color-text-inverse:       255 255 255;  /* #FFFFFF  text on primary fills */

  --color-border-subtle:      236 238 242;  /* #ECEEF2  card, panel, rail, table-row borders */
  --color-border-input:       221 225 232;  /* #DDE1E8  input and secondary-button borders */
  --color-border-pill:        229 231 235;  /* #E5E7EB  status-pill borders */
  --color-border-strong:      203 208 218;  /* #CBD0DA  resizable and emphasised boundaries */

  --color-bg-page:            245 246 248;  /* #F5F6F8  content column background */
  --color-surface:            255 255 255;  /* #FFFFFF  cards, rail, panels, inputs, modals */
  --color-surface-sunken:     249 250 251;  /* #F9FAFB  disabled inputs, read-only wells */

  /* ---- Semantic: dot = indicator, text = label/figure, tint = pill fill only ---- */
  --color-danger-dot:         240 68 56;    /* #F04438 */
  --color-danger-text:        217 45 32;    /* #D92D20 */
  --color-danger-tint:        254 243 242;  /* #FEF3F2 */

  --color-warning-dot:        247 144 9;    /* #F79009 */
  --color-warning-text:       181 71 8;     /* #B54708 */
  --color-warning-tint:       255 250 235;  /* #FFFAEB */

  --color-success-dot:        23 178 106;   /* #17B26A */
  --color-success-text:       6 118 71;     /* #067647 */
  --color-success-tint:       236 253 243;  /* #ECFDF3 */

  --color-info-dot:           41 112 255;   /* #2970FF */
  --color-info-text:          23 92 211;    /* #175CD3 */
  --color-info-tint:          239 248 255;  /* #EFF8FF */

  /* ---- Overlay ---- */
  --color-scrim:              16 24 40;     /* #101828 at 45% for modal backdrops */
}
```

### 4.2 The rationing rule

A screen is **90% white, page-grey, and near-black.** Violet appears once or twice as the unambiguous call to action or the active location. Red, amber, green, and blue appear only as a 6px dot, a single figure, or a pill tint.

Prohibited without exception:

- A filled red, amber, or green button, card, banner, or table row.
- A violet page region, violet card background, or violet block of body text.
- More than one primary (filled violet) button visible in a screen region at one time.
- Colour as the sole carrier of any meaning.

### 4.3 The `#98A2B3` correction

The reference uses `#98A2B3` for placeholder text, timestamps, counts, and metadata. Measured against `#FFFFFF` that is **2.58:1** — it fails WCAG 2.2 AA (4.5:1) for normal text, which Release 1 requires.

Rule:

- `--color-text-faint` (`#98A2B3`, 2.58:1) is permitted **only** for non-text decoration: icon glyphs that sit beside an already-legible text label, divider glyphs, and disabled-control chrome. It may never carry information that is not also present in a compliant colour.
- `--color-text-muted` (`#6B7280`, **4.83:1**) replaces it for every piece of small meaningful text: placeholders, timestamps, filter counts, table metadata, helper captions, "3 of 7 documents received".
- The visual weight difference is nearly imperceptible at 12–13px; the compliance difference is total.

### 4.4 Contrast ledger

Measured against `#FFFFFF`. Implementations must not introduce a foreground/background pair that is not in this table or independently verified.

| Foreground | Ratio on white | Verdict |
|---|---:|---|
| `#101828` text-primary | 17.77:1 | AAA |
| `#525C6E` text-secondary | 6.74:1 | AA (all sizes) |
| `#6B7280` text-muted | 4.83:1 | AA (all sizes) |
| `#98A2B3` text-faint | 2.58:1 | **Fails** — decoration only |
| `#5B2AE8` primary | 7.14:1 | AA all sizes; also the ratio of white-on-primary buttons |
| `#7C57EE` primary-accent | 4.71:1 | AA, thin margin — use at ≥13px weight 600, never for body copy |
| `#D92D20` danger-text | 4.83:1 | AA |
| `#B54708` warning-text | 5.43:1 | AA |
| `#067647` success-text | 5.69:1 | AA |
| `#175CD3` info-text | 5.99:1 | AA |

Focus rings, borders, and the 6px status dots are non-text; they must meet 3:1 against their adjacent surface. `#5B2AE8` on `#FFFFFF` satisfies this comfortably.

### 4.5 Gradients

Gradients are permitted in **exactly two** places and nowhere else:

1. The Assistant empty-state orb: `radial-gradient(circle at 32% 28%, #BCACF9, #5B2AE8 55%, #3C1AA3)`, `64px`, with `box-shadow: 0 12px 30px -8px rgb(91 42 232 / 0.55)`.
2. Chart bars: `linear-gradient(180deg, #8D6BF1, #5B2AE8)`.

Prohibited: gradient buttons, gradient text, gradient headers, gradient backgrounds, gradient borders, gradient cards, animated or mesh gradients, and any violet-to-indigo or violet-to-pink transition anywhere. See `iip_ui_craft_guardrails.md` §4 for why this restriction is load-bearing.

---

## 5. Typography

Family: `Inter, ui-sans-serif, system-ui, -apple-system, sans-serif`, loaded through `next/font` with `display: swap`. Base `font-size: 14px` set on `body`. `-webkit-font-smoothing: antialiased`. Enable `font-variant-numeric: tabular-nums` on every table cell, stat figure, currency value, timestamp, and identifier.

| Role | Size | Weight | Letter-spacing | Line-height | Colour |
|---|---:|---:|---|---|---|
| Page H1 | 23px | 700 | -0.41px | 1.25 | text-primary |
| Section / card title | 15px | 700 | 0 | 1.4 | text-primary |
| Modal title | 17px | 800 | -0.2px | 1.3 | text-primary |
| Stat figure | 30px | 700 | -0.5px | 1.1 | semantic or text-primary |
| Body, form values, chat copy | 14px | 400 | 0 | 1.6 | text-primary |
| Secondary / meta | 13px | 400–600 | 0 | 1.5 | text-secondary |
| Field label | 13.5px | 700 | 0 | 1.4 | text-primary |
| Eyebrow / column header | 11.5px | 700 | 0.05em, uppercase | 1.3 | text-muted or primary |
| Pill / badge | 12px | 500 | 0 | 1.3 | text-primary |
| Micro / footnote / disclaimer | 12px | 400 | 0 | 1.5 | text-muted |

Rules:

- Sentence case for every heading, label, button, and menu item. Uppercase only for eyebrows and table column headers.
- Never more than three type sizes in one card.
- No serif face. No display face. No monospace except for identifiers, hashes, JSON, and exact technical values — and even claim references (`CLM-2026-0184`) are set in Inter at weight 600 in `--color-primary`, matching the reference.
- Headings take negative tracking; eyebrows take positive tracking. That contrast is deliberate and is much of what makes the hierarchy read as engineered rather than default.

---

## 6. Spacing, radius, elevation, motion

### 6.1 Spacing

Base unit `4px`; the working rhythm is `8px`. Permitted gaps: `4, 6, 8, 10, 12, 14, 16, 20, 24, 28, 32`. Nothing else. Card interior padding is always `16px 20px`.

### 6.2 Radius

| Token | Value | Applies to |
|---|---:|---|
| `--radius-tight` | 4px | The tail corner of a chat bubble |
| `--radius-control` | 8px | Buttons, inputs, selects, rail buttons, small icon tiles |
| `--radius-chip` | 9px | AI avatar tile, agent icon tile, session icon |
| `--radius-card` | 12px | Every card, stat tile, list container. The most-used radius in the system. |
| `--radius-overlay` | 16px | Modals, popovers, the message composer, chat bubbles |
| `--radius-pill` | 9999px | Pills, badges, filter tabs, toggles, avatars, status dots |

Radius above `16px` is prohibited on any operational surface.

### 6.3 Elevation

Borders define structure; shadows do not. Static cards, panels, tables, and stat tiles carry `border: 1px solid --color-border-subtle` and `box-shadow: none`. Shadow is reserved for surfaces that genuinely float above the page:

| Token | Value | Applies to |
|---|---|---|
| `--shadow-popover` | `0 20px 40px -12px rgb(16 24 40 / 0.22)` | Popovers, dropdowns, command palette, toasts |
| `--shadow-modal` | `0 24px 60px -12px rgb(16 24 40 / 0.35)` | Modals, side drawers |
| `--shadow-primary` | `0 1px 2px rgb(91 42 232 / 0.25)` | Primary button only |

No hover-lift, no `transform: scale()` on hover, no glow, no coloured ambient shadow beyond the primary button's.

### 6.4 Motion

| Token | Value | Applies to |
|---|---|---|
| `--motion-fast` | `120ms cubic-bezier(0.2, 0, 0, 1)` | Hover tint, focus ring, toggle knob |
| `--motion-base` | `160ms cubic-bezier(0.2, 0, 0, 1)` | Popover/modal fade+scale in, panel expand |
| `--motion-exit` | `100ms ease-in` | Any dismissal |

Motion confirms state or location. It never entertains. Prohibited: entrance animations on page load, staggered list reveals, moving skeleton shimmer, parallax, scroll-triggered reveals, spinner-as-decoration, animated illustrations, and typing-effect text outside the live token stream of an actual model response. Honour `prefers-reduced-motion: reduce` by dropping to opacity-only changes at `80ms`.

### 6.5 Z-index scale

`10` sticky headers · `20` contextual panel overlays · `30` side drawer · `40` popover backdrop · `41` popover · `50` modal backdrop and modal · `60` toasts · `70` environment banner.

---

## 7. Iconography

- Library: `lucide-react` (already a dependency). No second icon set. No custom SVG except the product mark.
- Stroke width `1.8`. Rounded caps and joins. Never filled. Never multicolour. Always `currentColor`.
- Sizes: `14px` inline in buttons and pills · `16px` in nav rows and settings links · `19px` in the icon rail · `17px` inside a `34px` icon tile.
- Icon tiles: `26–38px` rounded square (`--radius-chip`) with `--color-primary-soft` background and `--color-primary` glyph for AI/agent surfaces, or `--color-bg-page` background and `--color-text-secondary` glyph for neutral surfaces.
- Every icon-only control carries an `aria-label` and a tooltip. Icon-only is permitted for the rail, table row actions, and composer utilities. It is **prohibited** for any consequential action — approve, reject, escalate, send, export, delete — which must always show a text label.

---

## 8. Component contracts

Each component below is normative. Build it once in `web/portal/components/ui/`, then reuse. If a screen needs something not listed here, extend this document before writing the component.

### 8.1 Primary button

`background --color-primary` · `color --color-text-inverse` · `font-size 13px` · `font-weight 700` · `padding 9px 16px` · `radius --radius-control` · `box-shadow --shadow-primary` · icon `14px` at `6px` gap. Hover `--color-primary-hover`. Active `--color-primary-active`. Focus-visible: `outline: 2px solid --color-primary; outline-offset: 2px`. Disabled: `opacity: 0.6`, `cursor: not-allowed`, and it stays violet — never greys out — so it reads as "not yet" rather than "broken". Exactly one per screen region.

### 8.2 Secondary button

`background --color-surface` · `border 1px solid --color-border-input` · `color --color-text-primary` · `font-weight 600` · same size and padding as primary. Hover: `background --color-bg-page`.

### 8.3 Ghost button

No border, no fill, `color --color-text-secondary`, weight 600. Only for the low-emphasis half of a pair (typically "Cancel" beside a primary "Save changes").

### 8.4 Destructive action

Text-only, `color --color-danger-text`, weight 700, `13px`. Never a filled red button. Any destructive or irreversible action opens a confirmation modal whose confirm button is the standard violet primary with an explicit verb ("Revoke session", "Delete draft").

### 8.5 Input

`background --color-surface` · `border 1px solid --color-border-input` · `radius --radius-control` · `padding 9px 12px` (`36px` left when a leading `15px` icon is present) · `font-size 14px`. Focus: `border-color --color-primary` plus `box-shadow: 0 0 0 3px rgb(91 42 232 / 0.12)`, no outline. Disabled/managed: `background --color-surface-sunken`, `color --color-text-muted`, a `12px` lock icon and a caption below explaining who controls it. Invalid: `border-color --color-danger-text` plus a message below in `--color-danger-text` at `12.5px` — never colour alone.

### 8.6 Field

`label` (13.5px/700) → optional hint (12.5px, `--color-text-muted`) → control → optional error or helper. Vertical gap between fields in a stack: `16px`.

### 8.7 Status pill

`inline-flex` · `border 1px solid --color-border-pill` · `radius --radius-pill` · `padding 3px 10px` · `background --color-surface` · text `12px/500` in **`--color-text-primary`** · a `6px` circular dot before the label carrying the semantic colour · `6px` gap.

The text is never coloured and the pill is never tinted. Status is encoded once, by the dot, plus the always-present word. This is the single most important consistency rule in the product: it is what keeps a screen full of bad news looking calm.

The one permitted tinted variant is a positive confirmation of a configured capability ("Enabled", "Connected"): `background --color-success-tint`, `color --color-success-text`, still with the dot.

### 8.8 Card

`background --color-surface` · `border 1px solid --color-border-subtle` · `radius --radius-card` · `padding 16px 20px` · `box-shadow: none`. Optional header row: title `15px/700` left, action or status right, `padding-bottom 14px`, `margin-bottom 14px`, `border-bottom 1px solid --color-border-subtle`.

A card means "a distinct object or a distinct action". Do not wrap every group in one. Prefer a bordered section or a plain stack when the content is not a separate object.

### 8.9 Stat tile

Same treatment as a card. Label `13.5px` `--color-text-secondary` on top, `8px` gap, figure `30px/700` below. The figure takes a semantic colour only when the number itself is the signal (`Requires review` in `--color-danger-text`, `Completed` in `--color-success-text`, `Exceptions` in `--color-warning-text`, an efficiency rate in `--color-primary`). A neutral count stays `--color-text-primary`.

Every stat tile must be a link into the filtered list it counts. A number the user cannot click through to is a decoration, and decorations are prohibited. A tile with a zero value renders `0` — never hidden, never an em dash.

### 8.10 Table

No zebra striping, no vertical rules, no outer border of its own beyond the containing card. `1px solid --color-border-subtle` under the header row and under every body row; the last row has none. Header cells `11.5px/700` uppercase `--color-text-muted` with `0.04em` tracking and `0 8px 10px` padding. Body cells `13.5px`, `12px 8px` padding, `vertical-align: middle`, producing a `54–66px` row. Numeric columns right-aligned with tabular numerals. Row hover: `background --color-bg-page`. Sortable headers show a `14px` chevron only on the active column.

### 8.11 Filter tabs

Pill-shaped, never underlined. Inactive: `background --color-surface`, `border 1px solid --color-border-input`, text `13px/600` `--color-text-primary`, count `12px/700` `--color-text-muted`. Active: `background --color-primary-soft-alt`, `border-color --color-primary-border`, text and count in `--color-primary-accent`. `padding 8px 14px`, `8px` gap. Every tab shows its count. A tab whose count is zero is still rendered, disabled at `opacity 0.55`.

### 8.12 Avatar

Circle. Initials in white at `11–12px/700`. Background colour chosen deterministically by hashing the user's stable id — never their name, and never random per render — from this fixed set: `#1D2939 #175CD3 #B54708 #6941C6 #0E7090 #067647 #B42318 #363F72`. The signed-in user's rail avatar carries a `9px` presence dot with a `2px` white ring.

### 8.13 Toggle

Track `36 × 20px`, radius `9999px`. Off: `#E4E7EC`. On: `--color-primary`. Knob `16px` white circle, `2px` inset, `box-shadow: 0 1px 2px rgb(0 0 0 / 0.2)`, transitions over `--motion-fast`. No text inside. The row's leading label is the accessible name; wire it with `role="switch"` and `aria-checked`.

### 8.14 Modal

Backdrop `rgb(16 24 40 / 0.45)`, centred. Panel `460px` (`92vw` max), `--color-surface`, `--radius-overlay`, `--shadow-modal`. Structure is fixed: header (`20px 22px`, title `17px/800`, one-line description `13.5px` `--color-text-secondary`, close button top-right) → hairline → body (`20px 22px`, fields stacked at `16px`) → hairline → footer (`16px 22px`, right-aligned, ghost "Cancel" then the primary confirm). Focus is trapped; `Escape` closes; focus returns to the trigger. Never nest a modal inside a modal.

### 8.15 Popover

`320px`, `--color-surface`, `--radius-overlay`, `--shadow-popover`, anchored to its trigger, with a transparent full-screen backdrop that closes it on click or `Escape`. Internally sectioned with `11px/700` uppercase group labels in `--color-text-muted`. Rows `8px 16px`, hover `--color-bg-page`, selected row shows a `14px` violet check on the right.

### 8.16 Side drawer

Right-anchored, `440px` (`92vw` max), full height, `--color-surface`, `--shadow-modal`, `border-left 1px solid --color-border-subtle`. Used for evidence, document preview, and audit detail — anything the user must consult *without leaving the decision they are making*. Same focus rules as a modal. The underlying page stays visible and inert.

### 8.17 Chat surface

**Empty state**, vertically centred: orb (§4.5) → greeting `26px/800` → subhead `15px` `--color-text-secondary` → suggestion chips. Nothing else. No feature grid, no "what can I do" accordion, no capability list.

**Suggestion chip:** `border 1px solid --color-border-input`, radius `10px`, `padding 10px 14px`, `13.5px/600`, a `15px` violet lucide icon at `8px` gap. Hover: `border-color --color-primary`. Maximum three, horizontally arranged, `max-width 520px` for the row. Their text must be generated from the signed-in user's actual pending work (navigation model §6.2) — never a hardcoded list.

**User message:** right-aligned, `background --color-primary`, white text `14px/600`, `padding 11px 18px`, `border-radius: 16px 16px 4px 16px`, `max-width 70%`.

**Assistant message:** left-aligned, preceded by a `26px` `--radius-chip` tile in `--color-primary-soft` holding a `14px` violet sparkle glyph. Bubble: `background --color-surface`, `border 1px solid --color-border-subtle`, `border-radius: 4px 16px 16px 16px`, `padding 16px 20px`, `14px/1.65`, `max-width 78%`. Supports bold, bullet lists, tables, inline citations (§8.20), and action cards (§8.21).

**Composer:** one `--radius-overlay` bordered box above the bottom edge, `padding 12px 14px`. Auto-growing textarea, no internal border. Utility icons (`17px`, `--color-text-faint`) bottom-left; send button bottom-right, `32px`, radius `--radius-control`, `#E4E1F7` while the field is empty and `--color-primary` the instant it is not. `Enter` sends, `Shift+Enter` newlines. Focus applies the same violet ring as an input to the whole box. A single centred `12px` `--color-text-muted` line sits beneath: *"IIP can make mistakes and does not make claim decisions. Verify before acting."*

**Conversation panel:** "New conversation" outlined button at the top, then a flat list of auto-titled conversations. Active row: `background --color-primary-soft-alt`, `color --color-primary`, weight 600, `3px` left border in `--color-primary`.

### 8.18 Toast

`sonner` (already a dependency), bottom-right, `380px` max. `background --color-surface`, `border 1px solid --color-border-subtle`, `--radius-card`, `--shadow-popover`. A `6px` semantic dot, title `13.5px/700`, one line of detail `12.5px` `--color-text-muted`, and at most one text action on the right in `--color-primary` weight 700. Auto-dismiss at `6s`; a toast carrying an action persists until dismissed. Never stack more than three; collapse the rest into "N more updates". What may and may not raise a toast is governed by `iip_ux_cognitive_load_and_navigation_model.md` §8 — that policy is binding, not advisory.

### 8.19 AI narrative card

The one place the rationing rule bends, and therefore the one place it must never be spent carelessly. Used at the top of a work item or claim detail to state, in plain English, what the automation did and what it needs from the human.

- Surface: `background #F9F5FF`, `border 1px solid #DCD1FB`, `--radius-card`, `padding 20px 22px`. We deliberately do **not** copy the reference's deep maroon fill: a large red block reads as an error, and triage output is a routing decision, not a failure.
- Eyebrow: `11.5px/700` uppercase, `--color-primary`, preceded by a `14px` icon. States what happened, factually: `EXTRACTION COMPLETE — 2 FIELDS BELOW CONFIDENCE THRESHOLD`.
- Headline: `17px/800` `--color-text-primary`.
- Body: two to four sentences, `14px/1.65`, first person and concrete, citing real counts and confidence values. "I read 6 documents and matched 5 against the motor own-damage checklist. The police abstract is missing, and the repair estimate scored 0.71 on total amount, below the 0.85 threshold."
- No watermark glyph, no oversized decorative background icon.
- It never states a decision, a conclusion of liability, or an amount to pay. See `iip_workflow_legibility_contract.md` §9.

Below it, a "Key findings" list: each row a `26px` icon tile, a `13.5px/700` finding, and an expandable detail region.

### 8.20 Evidence citation

Any AI-asserted fact that a human may act on renders as a chip immediately after the sentence: `background --color-bg-page`, `border 1px solid --color-border-subtle`, `radius --radius-pill`, `padding 2px 8px`, `11.5px/600`, `--color-text-secondary`, with a `12px` document icon. The label is the source document name plus page (`Police abstract · p.2`). Clicking it opens the side drawer at that source with the region highlighted. An assertion with no citation must not be rendered at all.

### 8.21 Action card (in chat)

When the assistant's answer resolves to work, the reply ends with a bordered card — not a link buried in prose — containing the object title, its status pill, two or three key facts, and a primary button that navigates to the object. This is the chat-to-work handoff and it is mandatory; see the navigation model §7.

### 8.22 Confidence indicator

Never a coloured bar alone. Render as the numeric value plus a word: `0.71 · Below threshold` in `--color-warning-text`, or `0.96 · High` in `--color-text-secondary`. Thresholds come from the backend and are never hardcoded in the portal.

### 8.23 Completeness indicator

Percentage `30px/700` in `--color-text-primary`, with `x of y required documents received` beneath in `13.5px` `--color-text-muted`, and the missing items listed by name. A bare percentage with no denominator and no missing list is prohibited. Full rules: `iip_workflow_legibility_contract.md` §7.

### 8.24 State surfaces

Every data surface implements six states, and none of them is a spinner alone.

| State | Treatment |
|---|---|
| Loading | Static skeleton blocks at `--color-surface-sunken`, no shimmer animation, matching the real layout's geometry. Under 400ms, render nothing rather than flashing a skeleton. |
| Empty (no data yet) | Centred in the card at `40px 20px`: one `13.5px` `--color-text-secondary` sentence saying what will appear here and, when applicable, one secondary button that creates the first one. No illustration. |
| Empty (filters excluded everything) | "No items match these filters." plus a ghost "Clear filters" button. Distinct from the state above — never show the same message for both. |
| Error | Inline within the card: `--color-danger-text` title, a plain-language sentence, a "Try again" secondary button, and a correlation id in `--color-text-muted`. Never surface a raw provider error, stack trace, or status code. |
| Not configured | A neutral pill reading `Not configured` and one sentence naming what an administrator must connect. Never a fake value, never a zero standing in for absent data. |
| No permission | State that access is restricted and who to contact. Never a blank screen, and never a 404 for something that exists. |

### 8.25 Bar chart

Vertical bars only, `linear-gradient(180deg, #8D6BF1, #5B2AE8)`, `border-radius 8px 8px 4px 4px`, bar width roughly equal to gap, `180px` plot height. Short axis labels in `--color-text-muted` `12px/600`. No gridlines, no axis lines, no legend for a single series, no 3D, no donut, no pie. Every chart is accompanied by an accessible table alternative and direct value labels on hover. Charts are ambient context; the table beneath them is the truth.

---

## 9. Dark mode

Out of scope for this release. The portal ships light-only.

Consequently, the Appearance setting must not offer a Dark or System option that does nothing — that would violate the honest-state invariant in `AGENTS.md`. Either omit the setting entirely or render the unavailable options disabled with a one-line explanation. Do not add a theme toggle before a complete dark token mapping, a chart and document-viewer audit, and an accessibility pass exist.

---

## 10. Migration map

The portal currently ships a blue-primary palette in `web/portal/app/globals.css` with a parallel HSL token family consumed through `web/portal/tailwind.config.ts`. Migrate in this order, keeping the app runnable at every step.

| Step | Change | Verification |
|---:|---|---|
| 1 | Replace the colour values in `app/globals.css` `:root` with §4.1. Keep every existing token *name* so no component breaks. Point `--color-primary` at violet and retire the separate `--color-ai` accent — AI provenance now uses the same violet at `--color-primary-soft`. | `pnpm build` |
| 2 | Move `--color-muted-soft` consumers to `--color-text-muted` per §4.3. Grep for `muted-soft` and `#98A2B3`. | `pnpm lint`, contrast spot-check |
| 3 | Introduce the radius, shadow, and motion tokens from §6 and remove ad-hoc `rounded-2xl`, `rounded-3xl`, `shadow-lg`, `shadow-xl` usages. | grep returns zero matches |
| 4 | Rebuild the shell: `components/iip/AppShell.tsx`, `Sidebar.tsx`, `TopBar.tsx` become the 64px rail plus contextual panel per §3 and navigation model §5. `TopBar` is removed as a fixed element. | `pnpm test` — update `check:ui-release-gates` in the same change, never bypass it |
| 5 | Build the §8 primitives in `components/ui/` and migrate `components/Badges.tsx`, `MetricCard.tsx`, `SectionCard.tsx`, `DataTable.tsx`, `PageHeader.tsx`, `States.tsx` onto them. | Storybook renders each state |
| 6 | Migrate `lovable-pages/*` screen by screen, highest-traffic first: `AIConversations` → `UniversalMyWork` → `ClaimReview` → `ClaimsQueue` → `Documents` → the rest. | Playwright visual suite re-baselined per screen |
| 7 | Delete the superseded token families and the parity-mode shims once no consumer remains. | grep + `pnpm build` |

Re-baselining visual snapshots is expected during this migration. Re-baseline **per screen, in the change that migrates that screen**, never in a bulk `--update-snapshots` sweep that hides regressions.

---

## 11. Definition of done for any UI change

A change is not done until all of these are true. This list is the reviewer's checklist.

1. Every colour, size, radius, and shadow comes from a token in this document. No literal hex in a component.
2. The screen renders correctly at 1280px, 1024px, and 768px, and reflows without horizontal scroll at 400% zoom / 320 CSS px.
3. All six states in §8.24 exist and were viewed, not assumed.
4. Keyboard: every control reachable in a sensible order, visible focus ring everywhere, `Escape` closes every overlay, focus returns to the trigger.
5. Screen reader: landmarks, one `h1`, correct heading order, table headers, accessible names on icon-only controls, status changes announced via a live region.
6. No colour-only meaning anywhere.
7. No emoji anywhere. See `iip_ui_craft_guardrails.md` §3.
8. No fabricated data, placeholder statistics, lorem ipsum, or sample names in a surface that can render in a protected runtime.
9. The workflow state shown matches the backend enum exactly, using the vocabulary table in `iip_workflow_legibility_contract.md` §10.
10. `pnpm lint`, `pnpm build`, and `pnpm test` pass, and the results are reported honestly.
