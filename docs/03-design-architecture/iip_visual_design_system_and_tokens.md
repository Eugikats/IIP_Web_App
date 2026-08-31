# IIP Visual Design System and Design Tokens

**Status:** Proposed visual source of truth extracted on 2026-08-04; measurable Release 1 acceptance is governed by the requirements/acceptance baseline as of 2026-08-05.

> Visual conformity does not establish accessibility. Critical Release 1 flows require WCAG 2.2 AA plus manual keyboard, screen-reader, focus, status, 200% text, and 400%/320 CSS-pixel reflow evidence under R1-A11Y-001–004.

**This document owns:** the visual source of truth — design tokens (color, typography, spacing, sizing, shape, elevation, motion, layering), layout, density, visual hierarchy, surfaces/borders/elevation, badges and status indicators, cards/panels/rails/drawers, application shell, Command Center visual treatment, tables and work queues, forms visual treatment, navigation visual treatment, data visualization grammar, AI interface visual patterns, system states and feedback, accessibility (visual), responsive design, dark mode, the component usage matrix, design-to-development guidance, responsive/density integration, design-tool and token delivery, and visual acceptance criteria.

**This document must not own:** IIP routes, Uganda insurance workflows, or product-specific screen behavior (see the IIP Platform UX Behavior Specification); or agentic AI interaction and governance patterns (see the Agentic AI Interface and Governance Patterns).

**Companion documents:**
- Foundation and governance — `iip_enterprise_agentic_operating_design_system.md`
- Agentic AI interface and governance patterns — `iip_agentic_ai_interface_and_governance_patterns.md`
- IIP platform UX behavior — `iip_platform_ux_behavior_specification.md`
- Recent Conversations feature — `iip_recent_conversations_session_continuity_spec.md`
- Release 1 surface/control behavior using these tokens — `iip_release_1_surface_control_interaction_contracts.md`

---

# Visual Design System

## Authority, Scope, and Maturity

This section is the visual source of truth for enterprise agentic products using this design system and for the IIP domain extension. It governs product design, prototyping, frontend implementation, design review, visual regression, accessibility review, and future component work. Product-specific UX rules in sections 03A and 03B govern behavior; this section governs how that behavior is visually expressed.

The system is implementation-ready at specification level. It has not been validated through participant research or a complete accessibility conformance test. Values marked **Decision required** need stakeholder approval before production adoption.

## Visual-Design Audit Summary

| ID | Existing issue | Enterprise impact | Resolution in this system |
|---|---|---|---|
| VIS-AUD-001 | Flat and Ambient Intelligence guidance coexist without one precedence rule. | Designers can select conflicting palettes, radii, shadows, and surfaces. | One semantic token model replaces both; migration aliases are documented. |
| VIS-AUD-002 | Glass, blur, glow, gradient buttons, and hover lift are treated as reusable operational defaults. | Decorative depth competes with status, evidence, and task hierarchy; translucency complicates contrast. | Operational surfaces are opaque and border-led. Gradients are limited to pre-auth/brand moments and a small brand mark. |
| VIS-AUD-003 | Radii range up to 24px and pill treatment is broadly available. | The product risks a soft consumer-app character and inconsistent containment. | Operational radius range is 0-12px; pills are restricted to tags, filters, and compact status labels. |
| VIS-AUD-004 | Token names mix raw CSS names, role names, HSL/RGB systems, and component-specific `--iip-*` variables. | Drift, hard-coded values, unclear ownership, and difficult theme support. | Primitive → semantic → component alias hierarchy with migration mapping and DTCG-compatible source. |
| VIS-AUD-005 | Semantic colors combine business state, AI state, workflow state, and risk. | Users may read AI confidence as claim truth or confuse escalation with branding. | Separate token families and patterns for status, risk, priority, approval, evidence, provider, and AI provenance. |
| VIS-AUD-006 | Monospace is prescribed for all financial values and many numbers. | Dense financial tables become harder to read and visually noisy. | UI font uses tabular numerals; monospace is limited to identifiers, code, hashes, and exact technical values. |
| VIS-AUD-007 | Layout sizes conflict: sidebar 240-264px versus 280px, rails 360px versus 380-440px, and large rounding varies by section. | Shell and responsive behavior cannot be implemented consistently. | Canonical shell, panel, drawer, modal, and density dimensions are defined once. |
| VIS-AUD-008 | Cards are the assumed container for metrics, chat blocks, records, governance, and dashboards. | Comparison suffers and page hierarchy becomes a field of equally weighted boxes. | Flat sections, tables, bordered groups, and split panes are defaults; cards require a distinct reusable object or action. |
| VIS-AUD-009 | Focus uses a soft violet shadow without guaranteed two-color contrast. | Focus can disappear on blue, violet, tinted, or dark surfaces. | Two-color focus ring with measurable contrast and no reliance on glow. |
| VIS-AUD-010 | Dark mode is declared supported but lacks a complete semantic mapping and QA gate. | Inversion may damage status meaning, documents, charts, and evidence review. | Dark tokens are defined; release remains gated until component, chart, document, and accessibility validation passes. |
| VIS-AUD-011 | Charts have a palette but lack thresholds, missing-data treatment, series limits, direct labels, and non-color encodings. | Operational trends can be misread or become inaccessible. | Restrained chart grammar, direct labels, patterns/shapes, table alternative, and data-quality states. |
| VIS-AUD-012 | Frontend guidance recommends packages not currently present in the portal. | Documentation can create implementation drift. | Guidance aligns with the inspected Next.js/React/Tailwind/Recharts/Lucide/Storybook/Playwright stack; new dependencies require approval. |

## Recommended Enterprise Visual Direction: Operational Intelligence

The visual direction is **Operational Intelligence**: a calm, light-first, border-led enterprise workspace with compact information hierarchy, stable geometry, restrained brand expression, and unmistakable human/AI/evidence states.

Ambient Intelligence is retained only as a brand signature: ink navy, primary blue, restrained violet AI provenance, and an optional subtle non-operational brand gradient. Glass panels, background blur, hover lift, glow shadows, gradient text, gradient operational buttons, and large translucent cards are removed from the canonical operating surface.

| Attribute | Interface expression | Prohibited expression |
|---|---|---|
| Trustworthy | Opaque surfaces, stable alignment, explicit state, visible source and ownership | Translucent evidence, hidden provenance, optimistic success |
| Calm | Low-saturation canvas, limited semantic color, rare motion | Neon accents, ambient animation, pulsing decoration |
| Precise | Tabular numerals, clear dividers, fixed column logic, exact timestamps on demand | Decorative numerics, ambiguous relative time in audit contexts |
| Intelligent | Contextual assistance, evidence adjacency, restrained violet provenance label | Glowing AI borders, purple page regions, agent avatars as personalities |
| Mature | Moderate radii, borders before shadows, concise type scale | Oversized headlines, excessive rounding, floating-card mosaics |
| Operational | Work, status, owner, SLA, evidence, and next action dominate | Marketing hero layouts, passive KPI walls, illustration-led empty states |

### Enterprise versus Consumer Expression

| Dimension | Enterprise rule |
|---|---|
| Saturation and quantity | One primary action color, one restrained AI cue, and semantic colors only where meaning exists. |
| Surfaces | Opaque, stable, and predominantly flat; no blur behind operational text or evidence. |
| Typography | Compact hierarchy, sentence case, tabular numerals, limited display scale. |
| Spacing | Dense enough for comparison; whitespace separates meaning rather than decorating emptiness. |
| Navigation | Persistent orientation and grouped destinations; no floating dock or icon-only ambiguity by default. |
| Components | Square-to-moderate geometry, visible boundaries, predictable controls. |
| Data | Tables and direct comparisons before cards and charts. |
| Iconography | Functional 16-20px stroke icons with text for consequential actions. |
| Gradients | Pre-auth/brand use only; never status, evidence, tables, approvals, or primary operational controls. |
| Illustration | Optional onboarding/empty-state support; never a substitute for instructions or operational data. |
| Motion | Confirms state, location, and continuity; never provides ambient spectacle. |
| Feedback | Inline and persistent for consequential states; toasts only for low-risk transient confirmation. |
| AI cues | Label, source, state, and boundary—not visual theatre. |

## 1. Visual-Design Principles

| Principle | Meaning and decision rule | Common mistake to avoid |
|---|---|---|
| Clarity before decoration | If ornament competes with status, evidence, ownership, or next action, remove it. | Adding gradients or glass to make a dense screen feel “AI-native.” |
| Operational usefulness | Every emphasized element must help orient, compare, decide, act, or recover. | Giving passive metrics the same weight as overdue work. |
| Visual restraint | Most surfaces use neutrals; color appears when it communicates action or state. | Tinting every card or section. |
| Hierarchy before containment | Establish reading order with semantics, alignment, type, and spacing before adding boxes. | Wrapping every group in a card. |
| Consistency with meaningful exceptions | Reuse tokens and patterns; permit deviation only when it communicates a stable difference in task or risk. | Creating a new component variant for one screen. |
| Accessible by default | Contrast, focus, reflow, semantics, and non-color meaning are component requirements. | Treating accessibility as a QA overlay. |
| Scalable system | Tokens represent roles, components compose, and patterns support volume and localization. | Hard-coding one-off sizes and colors. |
| Long-duration comfort | Avoid glare, animation, excessive contrast, and constant urgency; allow density choice where safe. | Using warning color as the default operational accent. |
| Trust and traceability | Human/AI authorship, evidence, freshness, and action state remain visible. | Styling AI output as authoritative system truth. |
| Progressive disclosure | Show task-critical content first; keep evidence and detail reachable without losing context. | Hiding required decision evidence behind nested dialogs. |
| Role and context awareness | Priority and available actions adapt to role; visual grammar and object names remain stable. | Giving each role a visually different product. |
| Human oversight | Consequential actions visually terminate in a human review and consequence preview. | Making an AI recommendation resemble an approved decision. |

## 2. Visual Character and Product Personality

Approved qualities: professional, institutional, calm, precise, sober, legible, evidence-led, structured, compact, responsive, and quietly intelligent.

Prohibited qualities: playful, glossy, neon, glass-heavy, gradient-heavy, cartoonish, social, lifestyle, gamified, excessively rounded, card-saturated, theatrically futuristic, or visually autonomous.

Whitespace is functional. Use it to separate task stages, evidence groups, and decision zones. Do not enlarge whitespace to imitate a marketing site. Brand expression is strongest in sign-in, product mark, and selected navigation; quieter in daily work; absent from critical errors, evidence, audit, print, and regulated decisions.

## 3. Design Tokens

### Token Architecture and Naming

Canonical flow: **primitive → semantic role → component alias → rendered value**.

Naming grammar: `category.role.variant.state`. Examples: `color.surface.default`, `color.action.primary.hover`, `size.control.standard`, `table.row.compact`, `motion.duration.fast`.

Primitive names such as `blue.700` may exist in the token source but must not be consumed directly by product components. Component aliases may reference semantic tokens; semantic tokens must never reference component aliases. The source format should be compatible with DTCG 2025.10 when a token pipeline is introduced.

### Core Color Tokens — Light

| Semantic token | Value | Use |
|---|---|---|
| `color.background.canvas` | `#F4F6F8` | Application background |
| `color.background.subtle` | `#F8FAFC` | Secondary region, alternating group |
| `color.surface.default` | `#FFFFFF` | Primary workspace and table |
| `color.surface.subtle` | `#F8FAFC` | Read-only or supporting region |
| `color.surface.sunken` | `#EEF2F6` | Code, comparison wells, selected source area |
| `color.surface.raised` | `#FFFFFF` | Menu, popover, drawer, modal |
| `color.text.primary` | `#111827` | Primary content |
| `color.text.secondary` | `#475569` | Supporting content |
| `color.text.tertiary` | `#64748B` | Metadata that must remain readable |
| `color.text.disabled` | `#94A3B8` | Disabled control only; never required instructions |
| `color.text.inverse` | `#FFFFFF` | Text on strong action surfaces |
| `color.border.subtle` | `#E5E9F0` | Internal dividers |
| `color.border.default` | `#D8DEE8` | Controls, tables, panels |
| `color.border.strong` | `#AAB4C3` | Resizable boundaries and emphasized groups |
| `color.action.primary` | `#1D4ED8` | Primary action and link |
| `color.action.primary.hover` | `#1E40AF` | Hover |
| `color.action.primary.active` | `#1E3A8A` | Pressed/active |
| `color.action.primary.subtle` | `#EFF6FF` | Selected background |
| `color.focus.inner` | `#FFFFFF` | Inner focus separation |
| `color.focus.outer` | `#1D4ED8` | Outer focus ring |
| `color.ai.provenance` | `#6D28D9` | AI label/icon only |
| `color.ai.surface` | `#FAF7FF` | Limited AI summary or draft region |
| `color.ai.border` | `#C4B5FD` | AI provenance boundary |

### Semantic, Risk, Priority, Approval, and Confidence Tokens

| Family/token | Foreground | Surface | Border | Rule |
|---|---|---|---|---|
| `status.success` | `#166534` | `#F0FDF4` | `#86EFAC` | Completed, healthy, delivered |
| `status.info` | `#1D4ED8` | `#EFF6FF` | `#93C5FD` | Informational or active processing |
| `status.warning` | `#92400E` | `#FFFBEB` | `#FCD34D` | Attention or review required |
| `status.error` | `#B91C1C` | `#FEF2F2` | `#FCA5A5` | Failed, rejected, invalid |
| `risk.critical` | `#7F1D1D` | `#FFF1F2` | `#FB7185` | Immediate high-severity risk only |
| `status.neutral` | `#475569` | `#F1F5F9` | `#CBD5E1` | Draft, inactive, archived |
| `approval.pending` | `#92400E` | `#FFFBEB` | `#FCD34D` | Human decision pending |
| `approval.approved` | `#166534` | `#F0FDF4` | `#86EFAC` | Human-approved outcome |
| `approval.rejected` | `#B91C1C` | `#FEF2F2` | `#FCA5A5` | Human-rejected outcome |
| `approval.escalated` | `#7F1D1D` | `#FFF1F2` | `#FB7185` | Escalated decision |
| `priority.low` | `#475569` | `#F8FAFC` | `#CBD5E1` | Low priority |
| `priority.normal` | `#1D4ED8` | `#EFF6FF` | `#93C5FD` | Normal priority when explicit label is needed |
| `priority.high` | `#92400E` | `#FFFBEB` | `#FCD34D` | High priority |
| `priority.urgent` | `#B91C1C` | `#FEF2F2` | `#FCA5A5` | Immediate action required |
| `confidence.sufficient` | `#334155` | `#F8FAFC` | `#CBD5E1` | Confidence is supporting metadata, not success |
| `confidence.review` | `#92400E` | `#FFFBEB` | `#FCD34D` | Human verification required |
| `confidence.insufficient` | `#B91C1C` | `#FEF2F2` | `#FCA5A5` | Cannot support proposed use |

### Dark Mode Semantic Equivalents

| Token | Dark value |
|---|---|
| `color.background.canvas` | `#0B1220` |
| `color.background.subtle` | `#0F172A` |
| `color.surface.default` | `#111827` |
| `color.surface.subtle` | `#172033` |
| `color.surface.sunken` | `#0B1220` |
| `color.surface.raised` | `#1E293B` |
| `color.text.primary` | `#F8FAFC` |
| `color.text.secondary` | `#CBD5E1` |
| `color.text.tertiary` | `#94A3B8` |
| `color.border.subtle` | `#263247` |
| `color.border.default` | `#334155` |
| `color.border.strong` | `#64748B` |
| `color.action.primary` | `#60A5FA` |
| `color.action.primary.hover` | `#93C5FD` |
| `color.action.primary.subtle` | `#172554` |
| `color.focus.inner` | `#0B1220` |
| `color.focus.outer` | `#93C5FD` |
| `color.ai.provenance` | `#C4B5FD` |
| `color.ai.surface` | `#241A3A` |
| `color.ai.border` | `#7C3AED` |

Dark semantic status surfaces must be independently contrast-tested; do not algorithmically invert light colors.

### Typography Tokens

| Token | Size/line | Weight | Tracking | Use |
|---|---:|---:|---:|---|
| `type.display` | `36/44px` | 650 | `-0.02em` | Pre-auth or executive presentation only |
| `type.page-title` | `28/36px` | 650 | `-0.015em` | Page identity |
| `type.section-title` | `22/30px` | 650 | `-0.01em` | Major section |
| `type.subsection-title` | `18/26px` | 600 | `0` | Subsection |
| `type.component-title` | `16/24px` | 600 | `0` | Panel/card/table title |
| `type.body` | `14/21px` | 400 | `0` | Default UI text |
| `type.body-strong` | `14/21px` | 600 | `0` | Important body text |
| `type.small` | `13/18px` | 400 | `0` | Dense cells and support text |
| `type.label` | `13/18px` | 600 | `0` | Form and control label |
| `type.metadata` | `12/16px` | 400 | `0.01em` | Timestamp, source, secondary metadata |
| `type.overline` | `11/16px` | 650 | `0.06em` | Rare category label; uppercase allowed |
| `type.metric` | `28/34px` | 650 | `-0.015em` | Primary KPI only |
| `type.code` | `12/18px` | 450 | `0` | Code, hash, correlation ID |

Font family: `Inter, "Segoe UI", system-ui, sans-serif`. Technical identifiers: `"IBM Plex Mono", "Cascadia Code", ui-monospace, monospace`. Use tabular figures through `font-variant-numeric: tabular-nums`; do not switch ordinary currency and percentages to monospace.

### Spacing, Sizing, Shape, Elevation, Motion, and Layering Tokens

| Category | Tokens and exact values |
|---|---|
| Spacing | `space.0=0`, `space.0.5=2px`, `space.1=4px`, `space.1.5=6px`, `space.2=8px`, `space.3=12px`, `space.4=16px`, `space.5=20px`, `space.6=24px`, `space.8=32px`, `space.10=40px`, `space.12=48px`, `space.16=64px` |
| Controls | compact `28px`, standard `36px`, comfortable/touch `44px`; multiline minimum `88px` |
| Icons | `12px` micro, `16px` default, `20px` prominent, `24px` standalone; stroke `1.75px` |
| Avatars | `24px`, `32px`, `40px`; avoid avatars for AI agents in high-stakes surfaces |
| Shell | header `56px`; sidebar expanded `248px`; collapsed `56px`; evidence rail `360px`, range `320-440px` |
| Drawers | narrow `360px`; standard `480px`; review `min(640px, 90vw)` |
| Modals | small `480px`; standard `640px`; large `800px`; max height `min(80vh, 880px)` |
| Content | reading `760px`; form `720px`; standard page `1280px`; operational page fluid with `24px` margins |
| Radius | `radius.none=0`, `radius.xs=2px`, `radius.sm=4px`, `radius.md=6px`, `radius.lg=8px`, `radius.xl=12px`, `radius.full=9999px` restricted to tags/chips |
| Borders | hairline `1px`; strong `2px`; selected/accent edge `3px`; no fractional borders |
| Elevation | `none`; `raised=0 1px 2px rgba(15,23,42,.08)`; `popover=0 8px 24px rgba(15,23,42,.14)`; `modal=0 16px 40px rgba(15,23,42,.18)` |
| Motion duration | instant `0ms`, fast `80ms`, standard `140ms`, deliberate `200ms`, slow `240ms` maximum for layout transitions |
| Motion easing | standard `cubic-bezier(.2,0,0,1)`; exit `cubic-bezier(.4,0,1,1)` |
| Layers | base `0`, sticky `100`, header/sidebar `200`, drawer `400`, menu/popover `500`, overlay `600`, modal `700`, toast `800`, critical alert `900` |

Borders separate persistent regions. Shadows are reserved for temporary overlap. Never stack more than two contained surface levels inside a page region.

## 4. Color System and Semantic Usage

- Brand blue identifies navigation selection, links, and the single primary action.
- Violet identifies AI provenance or a user-invoked AI capability; it never means approval, priority, risk, or success.
- Green means completed/approved/healthy only; red means error/rejection/urgent harm only; amber means attention/review/approaching threshold.
- Selected rows use primary subtle background plus a 3px leading edge or selection control.
- Hover uses neutral or primary-subtle background without moving the component.
- Disabled controls use reduced contrast only when their label and reason remain available nearby.
- Locked and read-only are neutral patterns with lock/read-only text; they are not disabled-color clones.
- Automated, human-reviewed, and approved are separate labels. Automation never inherits success color automatically.
- Color is never the only signal: use text, icon/shape, position, and programmatic state.
- No gradient operational buttons, large tinted cards, decorative status color, glow, neon, or purple page backgrounds.

## 5. Typography System

| Role | Token | Alignment/wrapping rule |
|---|---|---|
| Page title | `type.page-title` | Left aligned; one line preferred, two maximum |
| Page description | `type.body` + secondary color | Maximum 72 characters per line |
| Section/subsection | respective title token | Reflect semantic heading order; no skipped levels |
| Component heading | `type.component-title` | Wrap to two lines; do not truncate critical meaning |
| Body/support | `type.body` / `type.small` | 45-80 characters per line for prose |
| Form label | `type.label` | Persistent, sentence case, never placeholder-only |
| Table header/cell | `type.label` / `type.small` | Header wraps only where column contract permits |
| KPI | `type.metric` | Tabular figures; unit adjacent; freshness below |
| Identifier | `type.code` | Preserve full value in accessible name and copy action |
| Audit timestamp | `type.metadata` | ISO 8601 or tenant-approved exact format with timezone |

Dates use tenant locale for operational display and ISO 8601 for audit/export. Currency always includes ISO currency code when ambiguity is possible: `UGX 1,250,000`. Percentages state denominator/context where needed. Negative values use minus sign and accessible text; parentheses may be added for accounting views. Avoid centered body text and justified text.

## 6. Layout System

Use a 12-column fluid grid above 1280px, 8 columns from 1024-1279px, and 4 columns below 1024px. Default gutters: 24px desktop, 16px tablet, 12px narrow. Operational pages may be full width; prose and forms use maximum widths.

| Surface | Canonical layout |
|---|---|
| Command Center | Conversation-first center with optional 360px evidence rail; recent history in navigation/drawer |
| My Work/work queue | Full-width table; optional 360-440px preview drawer |
| Record detail | Header + next-action band + two-column detail at wide widths; tabs/sections below |
| Document review | Resizable 50/50 split; source minimum 420px and fields minimum 360px |
| Human review | Evidence/record comparison plus sticky decision region; no nested modal |
| Configuration | 720px form column with contextual help; summary/impact panel where necessary |
| Reports | Definition/freshness header, restrained visuals, underlying table and drill-down |
| Dashboard/oversight | Exception-first table and trends; metrics are compact summary strip, not card wall |

Avoid page-level nested scrolling. The shell owns vertical height; one primary content region scrolls. Sticky headers and action bars must not obscure focus at 200% zoom. Splitters are keyboard operable, expose current size, and retain user state where policy permits.

## 7. Layout Density

| Mode | Row | Input | Button | Cell padding | Icon | Use |
|---|---:|---:|---:|---|---:|---|
| Compact | `32px` | `28px` | `28px` | `4px 8px` | `14-16px` | High-volume tables/queues on pointer desktop only |
| Standard | `40px` | `36px` | `36px` | `8px 12px` | `16px` | Command Center, record detail, default operations |
| Comfortable | `48px` | `44px` | `44px` | `12px 16px` | `18-20px` | Touch, onboarding, low-volume settings and executive review |

Compact mode does not reduce body text below 13px or interactive target availability below WCAG requirements; use target padding or row activation rules. Document review may use compact fields but standard action controls. Mobile/touch uses comfortable controls. Density preference cannot hide fields, states, or required actions.

## 8. Visual Hierarchy

| Element | Visual treatment | Placement and competition rule |
|---|---|---|
| Required action | body-strong, primary/danger control as consequence requires | Near evidence and decision; never compete with passive KPI |
| Critical alert | critical text/icon, 3px leading edge, concise neutral surface | Top of affected region; one critical alert cluster |
| Page identity | page title + identifier/status cluster | First content row; not repeated as a card |
| Primary metric | metric type, primary text, unit and freshness | Summary strip; maximum 3 primary metrics above fold |
| Status/risk/priority | compact labeled indicator | Adjacent to object or task; never in decorative corner |
| Section/component heading | section/component token | Precedes content; no colored banner by default |
| AI-generated content | normal content type + explicit violet provenance label | Same hierarchy as content role, not automatically elevated |
| Human-reviewed content | review label, reviewer, timestamp | Adjacent to reviewed output |
| Audit information | small/metadata, exact values, expandable | Lower hierarchy but always discoverable |
| Identifiers/timestamps | code/metadata | Stable positions; never primary unless task is identifier lookup |

The intended operational scan path is: identity and state → required action → critical facts/evidence → available actions → supporting history. Temporary urgency may interrupt this path only when user action is required.

## 9. Surfaces, Borders, Elevation, and Containment

| Container | Use when | Do not use when |
|---|---|---|
| Flat section | Content belongs to the page and can be separated by heading/spacing | A reusable independent object needs selection or actions |
| Divider | Separating sequential or comparable groups | Creating visual rhythm without semantic boundary |
| Bordered group | Fields/evidence form one meaningful unit | Nesting inside another bordered card without need |
| Card | A reusable, independently actionable summary object | Displaying every section, metric, or table row |
| Panel/rail | Persistent supporting context related to primary work | It duplicates the main workspace |
| Drawer | Temporary preview/edit while preserving page context | Consequential review or long multi-step work |
| Modal | Short blocking confirmation or focused choice | Browsing evidence, long forms, nested decisions |
| Inline expansion | Detail belongs to the current row/section and comparison should remain visible | Content is too large or changes navigation context |

Maximum containment: page → panel/bordered group → inline subregion. Do not nest card inside card inside drawer. Use borders for persistent structure, shadow for overlap, and radius only at the outer boundary.

## 10. Badges, Chips, Tags, and Status Indicators

| Pattern | Purpose | Anatomy/behavior |
|---|---|---|
| Status badge | Lifecycle state | 20-24px height, radius full, icon optional, 1-3 words, noninteractive |
| Priority marker | Work ordering | Text + shape/leading bar; appears with reason on demand |
| Risk marker | Reviewed risk condition | Explicit level and subject; never implies fraud finding |
| Category tag | Taxonomy | Neutral outline; removable only in edit context |
| Filter chip | Active filter | Interactive, clear label/value, removable button with accessible name |
| Role/permission label | Current scope | Neutral; avoid badge clouds |
| AI provenance label | Authorship/source type | `AI generated`, `AI extracted`, or `AI proposed`; violet icon/text, neutral surface |
| Human-review label | Review state | Reviewer/status/timestamp; success only after actual approval where applicable |
| SLA indicator | Time state | Remaining/breached text plus clock icon; urgency color only at threshold |
| Confidence indicator | Output uncertainty | Text level + explanation/source quality; not a progress ring or outcome color |

Badges do not contain primary actions. Truncate only after preserving meaning; expose full label accessibly. Do not stack more than three badges in a row—move additional metadata to a details region.

## 11. Cards, Panels, Rails, and Drawers

Canonical anatomy: header (title/state), body (task-relevant content), optional metadata, and footer actions only when the whole object is independently actionable. Default padding is 16px standard, 12px compact, 20px comfortable.

| Variant | Required content | Rules |
|---|---|---|
| Summary card | Title, one summary, optional link | No chart plus multiple actions plus long prose |
| Metric card | Value, unit, definition, period/freshness | Use only for 1-3 decision-relevant metrics; prefer metric strip |
| Record card | Identifier, state, owner, next action | Use on narrow/touch; table preferred at volume |
| Action card | Proposed action, consequence, owner, state | Must show whether proposed/current/completed |
| Exception card | Issue, severity, affected scope, owner, next action | Use only for discrete exception; otherwise queue row |
| Recommendation card | AI label, recommendation, sources, limitations, actions | Never use approval styling |
| Context rail | Current record, evidence, source, freshness | Collapsible, state preserving, one scroll region |
| Preview drawer | Read-only preview and quick low-risk action | Width 480px; deep link available |
| Edit drawer | Short bounded edit | Preserve input; no consequential multi-stage decision |
| Review drawer | Medium-risk review only | High-risk review uses full cockpit |

## 12. Application Shell

| Region | Specification |
|---|---|
| Global header | 56px, opaque surface, bottom border, sticky; workspace, search, notifications, help, user menu |
| Sidebar | 248px expanded/56px collapsed; opaque surface; right border; grouped navigation; selected item uses subtle blue background + 3px leading edge |
| Environment indicator | Persistent text label outside Production; amber for Staging, blue-grey for Development, violet outline for LocalTest only; no Demo protected runtime |
| Page header | 64-88px content-dependent; title, safe description, status/owner, primary action; bottom divider for dense pages |
| Breadcrumbs | Detail/review/admin/audit; small text, current page not linked |
| Search | 320-560px desktop; keyboard shortcut; full-width overlay below 768px |
| Intelligence rail | 360px default, resizable 320-440px; border-left; never obscures primary action |
| System status | Contextual banner for degraded capability; persistent footer/status bar only if operations require always-visible state |

Collapsed navigation icons require tooltips and accessible names. Do not collapse automatically on ordinary desktop. The shell preserves route, filters, selected work item, and focus through panel toggles.

## 13. Command Center

The Command Center is a chat-first operational decision surface. It is not a dashboard grid.

Priority above the fold:

1. active workspace/context and conversation title;
2. material warning or changed-evidence notice;
3. latest user request and grounded response;
4. current proposed action or next step;
5. composer;
6. optional evidence rail when evidence is selected.

| Content | Visual form |
|---|---|
| Operational overview | Structured briefing block with compact metric strip and timestamp |
| Exceptions/tasks/approvals | Table or queue block with 3-7 preview rows and `Open all` |
| Risks/escalations | Inline alert plus affected-item table; no red metric card wall |
| SLA/workload | Compact summary + table; chart only for trend or distribution |
| Agent activity | Timeline/task block behind disclosure unless waiting/failure affects user |
| System health | Contextual degraded banner; full diagnostics in Governance/Admin |
| Trends | One restrained chart with direct labels and underlying data link |
| Recommended action | Action proposal with source, consequence, permission, and human boundary |
| Recent changes | Change-summary timeline or list; material changes first |

Conversation content max width is 880px for prose and 1120px for embedded tables/charts. User messages use primary-subtle surface, not speech-balloon styling. Assistant output is a flat structured region with a provenance header—not a floating glass card. Refresh preserves the conversation and labels what data refreshed. Alerts appear near affected content and remain until resolved or acknowledged per policy.

## 14. Tables and Work Queues

Use a table when users compare the same attributes across four or more records, scan high volume, sort/filter, select, or perform bulk work. Use cards only when objects have heterogeneous content or touch layouts cannot preserve a useful table.

| Element | Rule |
|---|---|
| Header | 36px standard, sticky where useful, secondary surface, 13px/600, left aligned except numeric |
| Row | Density token; 1px divider; hover tint; selection control independent of row link |
| Numeric | Right aligned, tabular figures, unit in header or value |
| Status/priority/SLA | Labeled pattern; do not color the entire row except critical accessible leading indicator |
| Sticky columns | Selection + primary identifier only by default; show boundary shadow/line when scrolled |
| Sorting | One active sort by default; announce direction; multi-sort explicit |
| Filters | Summary bar, applied-count, clear-all, saved view; filter state survives navigation |
| Selection | Persistent count and scope; bulk actions name affected count and eligibility |
| Pagination | Server-driven for high volume; show range and total when reliable |
| Column configuration | User-specific where permitted; reset to role default |
| Preview | Drawer retains row/table position; full route available |
| Inline editing | Low-risk isolated values only; explicit save/undo and concurrency check |
| AI priority | Small AI provenance cue + human-readable reason; never reorder invisibly |

Loading uses row skeletons with stable columns. Partial/error states identify affected region and preserved filters. Empty states distinguish no data, no filter matches, no permission, and provider not configured.

## 15. Forms and Data Entry

- Labels sit above controls; required/optional state appears in the label, not color or placeholder.
- Default field gap is 16px; related compact fields may use 12px; section gap is 24-32px.
- Validate format on blur, cross-field/business rules on review/submit, and server authority on save.
- Error text names the problem and recovery and is programmatically linked.
- Read-only fields use normal readable text on subtle surface; disabled controls are for unavailable interaction, not display.
- Locked fields show owner/reason and update path.
- Long forms use logical sections, save draft, progress through named steps only when dependencies require sequence, and preserve valid input.
- Review screens show entered, derived, and source values; highlight differences without color alone.
- Autosave shows `Saving`, `Saved {time}`, or `Not saved`; never show success before server confirmation.
- Unsaved-change warnings appear only when data would be lost; navigation recovery returns to the draft.

## 16. Navigation

Global navigation uses `iip_information_architecture_navigation_and_screen_inventory.md`. Local tabs belong to the current record; breadcrumbs show parent context; contextual links connect related records; overlays do not become navigation destinations.

Selected state uses position, 3px leading edge, subtle background, and `aria-current`. Hover is not selection. Back returns to the actual prior work context; breadcrumbs return to hierarchy. Record-to-record next/previous appears only inside a stable filtered queue and preserves queue order. Mobile uses a full-height navigation drawer; it does not flatten grouped destinations or hide current workspace.

## 17. Data Visualization

| Token/series | Value | Use |
|---|---|---|
| `chart.series.1` | `#1D4ED8` | Primary series |
| `chart.series.2` | `#0F766E` | Secondary series |
| `chart.series.3` | `#7C3AED` | Third/AI-related series only when meaning fits |
| `chart.series.4` | `#B45309` | Fourth series |
| `chart.series.5` | `#475569` | Fifth series |
| `chart.grid` | `#D8DEE8` | Major gridlines only |
| `chart.axis` | `#64748B` | Axis and label text |
| `chart.missing` | `#CBD5E1` + hatch | Missing/not available |
| `chart.forecast` | Series color at 55% + dashed line | Forecast |
| `chart.confidence` | Series color at 15% fill | Confidence range with text explanation |

Maximum five categorical series; maximum four overlapping line series. Prefer direct labels; legends must support keyboard/screen reader when interactive. Donut charts are limited to 2-4 stable parts; never use for precise comparison. Heatmaps require table alternative and textual extremes. Missing data uses gaps/hatching, never zero. Anomalies use marker shape plus label. Every chart shows title, measure, time period, filters, freshness, source, and view-data action. If a number, sentence, or table communicates the decision better, do not use a chart.

## 18. AI Interface Patterns

| State/authorship | Required visual treatment |
|---|---|
| AI-generated | Violet provenance icon/label, timestamp, sources, limitations; normal content typography |
| System-generated | Neutral system label; no AI color |
| Human-authored | Author/role where relevant; no special color |
| Human-reviewed | Review label, reviewer, timestamp, review outcome |
| Approved | Approval status pattern only after recorded human decision |
| Unverified | Warning/neutral evidence pattern and action restrictions |

Suggestions are secondary actions. AI summaries use headings, claims with adjacent citations, and limitations. Confidence is text plus reason/source quality, not a colorful gauge. Evidence opens in the context rail or review split. Correction/override preserves previous and proposed values, captures reason where required, and writes audit. Agent activity uses task/timeline patterns. No glow, animated particles, typing theatre, gradient AI headers, personality avatars, or purple containers around every response.

## 19. System States and Feedback

| State | Pattern |
|---|---|
| Loading | Skeleton matching final structure; spinner only for compact control/action |
| Empty | Inline region with cause and valid next action; no fabricated examples |
| No results | Preserve query/filters; show clear filters/change search |
| Error | Inline affected region; problem, preserved state, recovery, correlation reference where useful |
| Warning | Persistent inline alert near affected action |
| Success | Inline confirmation for consequential completion; toast for low-risk transient completion |
| Offline | Top contextual banner + per-action state; server-authoritative actions disabled |
| Syncing/synced | Text state near saved artifact; avoid continuous green badges |
| Partial failure | Summary plus item-level failure and retry scope |
| Permission denied | Full or regional state without restricted data leakage |
| Session expired | Re-authentication flow preserving safe work where possible |
| Locked/read-only | Neutral banner/field state with reason and owner |
| Maintenance/degraded | Persistent banner with affected capabilities and safe alternatives |
| Critical outage | Global alert below header; block only affected operations |

Toasts auto-dismiss only for low-risk information and remain accessible long enough to perceive. Modals are reserved for irreversible or high-consequence confirmation. Progress percentages appear only when measurable; otherwise show named step and elapsed status.

## 20. Accessibility

WCAG 2.2 Level AA is the minimum target. This specification does not itself establish conformance.

- Normal text contrast at least 4.5:1; large text at least 3:1; UI components and meaningful graphics at least 3:1.
- Text supports 200% resize; layouts reflow at 400% zoom without loss of critical content or two-dimensional scrolling except essential tables/graphics.
- Focus uses `2px color.focus.inner` plus `2px color.focus.outer`, offset `1px`; it remains visible on all theme/state surfaces and is not obscured by sticky regions.
- All operations work by keyboard; focus order follows semantic/task order; skip links reach main content and major regions.
- Pointer targets meet WCAG 2.2 minimum and use 44px touch targets for primary touch workflows.
- Status, risk, priority, confidence, AI authorship, chart series, and selection never rely on color alone.
- Forms provide persistent labels, instructions before action, linked errors, error summary for long forms, and prevention/review for legal or financial submission.
- Status updates use appropriate live regions without excessive announcements; loading does not repeatedly announce.
- Reduced motion removes movement and uses instant/opacity state change; no flashing or ambient animation.
- High-contrast/forced-colors mode preserves boundaries, focus, selection, and icons using system colors.
- Charts provide textual summary and accessible data table; tooltips are keyboard/focus accessible and dismissible.
- Manual verification includes keyboard, NVDA/JAWS on Windows, VoiceOver where supported, zoom/reflow, forced colors, reduced motion, touch, and representative disabled-user testing for critical flows.

## 21. Responsive Design

Breakpoints are implementation triggers, not device identities: narrow `<768px`, medium `768-1023px`, operational `1024-1279px`, wide `1280-1599px`, extended `≥1600px`.

| Region/pattern | Wide/extended | Operational | Medium | Narrow |
|---|---|---|---|---|
| Shell navigation | 248px persistent, collapsible | 56px rail or 248px based on task | Drawer, state preserved | Full-height drawer |
| Evidence rail | 360px, resizable | 320-360px overlay or split | Full-height drawer/tab | Separate route/sheet |
| Command Center | Conversation + optional rail | Conversation + overlay rail | Single conversation, evidence drawer | Single column; composer above virtual keyboard |
| Work queue | Full table, sticky columns | Table with column priority | Table horizontal scroll + preview route | Priority card/list or dedicated table view; no unusable compression |
| Record detail | Two-column + tabs | Two-column where fit | Single column with sticky action bar | Summary/task support only |
| Document review | Resizable split | Split if ≥1024px | Toggle source/fields retaining position | Upload/status only; complex correction unavailable unless validated |
| Human review | Side-by-side evidence/decision | Full-page two region | Full-page sequential review | High-risk final action unavailable unless validated |
| Forms | 1-2 columns by dependency | 1-2 columns | Single column | Single column, 44px controls |
| Charts | Full chart + table link | Reduced labels | Horizontally scroll only if essential | Summary + table; simplified chart optional |

Orientation, resizing, zoom, and panel toggles preserve drafts, selection, focus target, filters, and reading position. Visual order must match DOM/focus order. Desktop-only workflows are document field correction at scale, bulk work, complex comparison, governance matrices, and high-risk final review until mobile validation proves otherwise. Mobile supports status, search, notifications, uploads, summaries, comments, and low-risk tasks.

## 22. Dark Mode

Dark mode is appropriate for optional long-duration monitoring and user preference, but it is **not Release 1 default** and must not launch until all components, charts, documents, status combinations, focus, print, and screenshots pass validation.

Use the dark semantic tokens rather than inversion. Documents and scanned evidence may remain on a neutral light canvas with clear boundary. Shadows become less useful; use border/luminance separation. Status hues need lighter foregrounds and low-luminance surfaces. Images, charts, logos, and extracted highlights require theme-specific treatment. Print/export always uses the light evidence theme.

## 23. Component Usage Matrix

| Pattern | Best use | Poor use | Density | Narrow behavior | Accessibility |
|---|---|---|---|---|---|
| Card | Independent heterogeneous object/action | Repeated comparable rows | Standard/comfortable | Becomes list item | One clear heading/action model |
| Table | High-volume comparison and bulk work | Heterogeneous narrative content | Compact/standard | Horizontal priority table or alternate list | Headers, caption, selection labels |
| List | Simple sequence/activity/results | Multi-column comparison | All | Retained | Semantic list and clear item action |
| Panel | Persistent grouped workspace region | Every page section | Standard | Reflows/route | Landmark/name where useful |
| Drawer | Preview/short edit preserving context | High-risk or long work | Standard | Full-height sheet/route | Focus management, close, return focus |
| Modal | Short blocking confirmation/choice | Evidence browsing or long form | Standard | Near-full screen | Dialog semantics and focus trap |
| Rail | Supporting evidence/context | Duplicate primary content | Standard | Drawer/route | Named complementary region |
| Tabs | Peer views of one object | Sequential steps | Compact/standard | Scrollable tab list or select only if tested | ARIA tabs and keyboard behavior |
| Accordion | Progressive optional detail | Primary navigation or required comparison | All | Retained | Button heading, expanded state |
| Chart | Trend/distribution/relationship | Single number or exact record lookup | Standard/comfortable | Simplify + table | Text summary and data alternative |
| Timeline | Ordered events and provenance | Unordered attributes | Compact/standard | Single column | Semantic ordered list |
| Queue | Owned actionable work | Passive reporting | Compact/standard | Priority list | State, owner, SLA, selection |
| Alert | Material state requiring notice/action | Decorative emphasis | Standard | Full width | Role/status, concise action |

## 24. Design-to-Development Guidance

### Current Implementation Alignment

The inspected portal uses Next.js 15, React 19, Tailwind CSS 3.4, Recharts, Lucide, Storybook, Playwright, and axe-core. This document does not authorize introducing Radix, TanStack Table, React Hook Form, Zod, D3, or another UI framework. Add dependencies only through an approved architecture decision and concrete need.

### Token Source and CSS Mapping

Recommended source:

~~~json
{
  "$schema": "https://www.designtokens.org/schemas/2025.10/format.json",
  "color": {
    "surface": {
      "default": { "$type": "color", "$value": "#FFFFFF" }
    },
    "action": {
      "primary": { "$type": "color", "$value": "#1D4ED8" }
    }
  }
}
~~~

CSS aliases:

~~~css
:root {
  --color-background-canvas: #F4F6F8;
  --color-surface-default: #FFFFFF;
  --color-text-primary: #111827;
  --color-border-default: #D8DEE8;
  --color-action-primary: #1D4ED8;
  --color-ai-provenance: #6D28D9;
  --size-control-standard: 36px;
  --radius-component-default: 6px;
}
~~~

Component properties use orthogonal names: `density="compact|standard|comfortable"`, `tone="neutral|info|success|warning|danger|critical|ai"`, `state="default|loading|error|disabled|readOnly"`, and task-specific variants only when behavior differs. Do not encode page names into generic component variants.

Reusable layout primitives: `AppShell`, `PageHeader`, `WorkspaceGrid`, `SplitView`, `ContextRail`, `StickyActionBar`, `ScrollableRegion`, `RecordHeader`, `MetricStrip`, and `EvidenceGroup`.

Every documented component must include purpose/non-purpose, anatomy, properties/defaults, allowed variants, states, interaction/event contract, content limits, localization, responsive behavior, token aliases, semantics/keyboard/focus/announcements, examples, anti-patterns, and tests.

### Existing Portal Migration

| Existing pattern | Canonical replacement |
|---|---|
| `.ambient-bg` radial gradient | `color.background.canvas`; optional subtle brand wash only on sign-in/pre-auth |
| `.glass`, `.glass-subtle`, `.glass-elevated` | Opaque surface + border; elevation only for overlay |
| `.hover-lift` | Static hover background/border; no translation |
| gradient primary buttons/text | Solid `color.action.primary`; gradient limited to brand mark/pre-auth |
| 16-24px operational radii | 4-8px component radii; 12px modal maximum |
| `--color-ai` as general accent | `color.ai.provenance` for AI origin only |
| duplicate HSL/RGB token trees | One semantic source with compatibility aliases during migration |

Do not mass-replace without component visual regression. Migrate shell and primitives, then tables/forms, then Command Center and review patterns, then secondary/admin surfaces. Remove compatibility aliases only after usage search and screenshot comparison pass.

### Documentation, QA, and Governance

- Storybook or equivalent documents every component, density, theme, state, long content, localization, and accessibility note.
- Playwright covers shell, Command Center, queues, document review, approval, forms, empty/error/NotConfigured, and responsive transformations.
- Visual regression viewports: 390×844, 768×1024, 1024×768, 1280×800, 1440×900, and 1920×1080, plus 200% zoom scenarios.
- Axe automation is required but does not replace manual keyboard/screen-reader/contrast/reflow review.
- Token lint rejects raw hex/radius/shadow values outside the token source and approved visualization exceptions.
- Design QA compares semantic hierarchy, not only pixels: content order, state, focus, density, overflow, and action consequence.
- Minor token additions require design-system owner review; semantic meaning changes are breaking; deprecate for one release before removal unless security/accessibility requires immediate replacement.
- Each component release records version, owner, affected consumers, migration, accessibility impact, and visual-regression result.

## Visual-Design Acceptance Criteria

- One semantic token source controls light/dark values, density, shape, elevation, motion, and component aliases.
- Operational screens use opaque surfaces, restrained color, borders before shadows, and no decorative gradient/glass/glow.
- Shell dimensions, panel widths, control heights, row heights, and radii match this section.
- Status, risk, priority, approval, confidence, evidence, provider, and AI origin remain distinguishable without color.
- Command Center remains conversation-first and does not become a KPI-card dashboard.
- Tables preserve comparison, keyboard access, selection scope, and honest partial/error states.
- Responsive transformations preserve priority and state rather than simply stacking desktop regions.
- WCAG 2.2 AA requirements are tested at component and complete-process level.
- Frontend documentation names real installed technology and does not imply unsupported dependencies.

## Visual-System Change Log

### Added

- Operational Intelligence direction and enterprise-versus-consumer rules.
- Full semantic token architecture with light/dark, status, risk, priority, approval, confidence, chart, typography, spacing, sizing, shape, elevation, motion, and layering values.
- Formal hierarchy, containment, density, shell, Command Center, table, form, navigation, visualization, AI, feedback, responsive, dark-mode, component-selection, implementation, and QA specifications.
- Portal migration map and DTCG-compatible token-source example.

### Rewritten or consolidated

- Former Product Shell, Core Color, Semantic Color, Typography, Spacing/Radius/Elevation, Navigation, Data Table, Status, Forms, Feedback, Accessibility, Data Visualization, AI Interface, Responsive/Density, Implementation Stack, and Figma/token guidance are consolidated here.
- Command Center visual behavior is aligned with the adaptive conversation-first UX in section 03B.

### Removed as canonical guidance

- Glass/translucent operational surfaces, blur, glow, hover lift, gradient text, gradient operational buttons, large ambient backgrounds, default 16-24px radii, unrestricted pill styling, and generic card-first layouts.
- Unverified package recommendations that are not installed in the portal.
- Monospace as the default for all currency/numeric values.

### Retained

- Light-first operating environment, navy/blue authority, restrained violet AI identification, Inter/Segoe UI family, 4px spacing foundation, WCAG 2.2 AA target, tabular numerals, chat-first Command Center, evidence visibility, human review, honest system states, and print/evidence requirements.

### Corrected inconsistencies

- Sidebar, rail, header, control, density, radius, shadow, focus, chart, and responsive values now have one canonical definition.
- AI color no longer overlaps approval, escalation, success, or risk meaning.
- Dark mode is specified but release-gated instead of assumed complete.

## Unresolved Visual Decisions Requiring Stakeholder Input

- Approve Operational Intelligence as the replacement operational expression for the prior glass-heavy Ambient Intelligence implementation.
- Confirm whether a subtle blue-violet brand gradient remains on sign-in/pre-auth and product mark only.
- Confirm licensed/hosted Inter availability and fallback metrics for customer-managed deployments.
- Decide whether dark mode is in Release 1 or remains post-release until validation passes.
- Confirm default density by role and whether users may persist a personal density preference.
- Confirm whether mobile may ever support high-risk final approvals after dedicated research and security review.
- Confirm the canonical token source location and whether DTCG JSON, CSS, or Figma variables are authoritative during migration.
- Confirm first supported localization set and whether bidirectional layout is required.

## Applied Design Skills and Contribution

| Skill | Contribution to this revision |
|---|---|
| UI/UX Solution Architect | Diagnosed the brief, selected the specialist sequence, and set the implementation-ready output contract. |
| UI/UX Product Design Orchestrator | Ordered audit, direction, architecture, system design, accessibility, and handoff work into one coherent pass. |
| Design Consistency Auditor | Found conflicting themes, dimensions, semantic colors, containment patterns, and duplicate guidance. |
| Brand Interface Translator | Converted trust, authority, restraint, and AI provenance into the Operational Intelligence visual character. |
| Information Architecture Designer | Consolidated the former visual sections into one canonical, navigable system without weakening the agentic operating model. |
| Design Token Variable Architect | Defined primitive, semantic, component, theme, density, and migration token contracts. |
| Visual Hierarchy Designer | Established page hierarchy, scan paths, emphasis limits, typography roles, and restrained color use. |
| Component System Designer | Defined anatomy, variants, states, composition rules, usage criteria, and component documentation requirements. |
| Complex UI Pattern Designer | Specified Command Center, work queues, split review, evidence, overlays, and high-density enterprise behaviors. |
| Responsive Adaptive UI Designer | Defined priority-preserving transformations, mobile capability boundaries, and state continuity across breakpoints. |
| Accessibility Design Auditor | Strengthened WCAG 2.2 AA, focus, contrast, reflow, target-size, non-color, motion, chart, and complete-process requirements. |
| Design Handoff Documentation Generator | Added exact values, mappings, migration sequence, acceptance criteria, QA gates, and stakeholder decisions for implementation. |

---

# 34. Visual System Integration: Responsive and Density

The canonical breakpoint, reflow, mobile-capability, and density rules are in **Visual Design System sections 7 and 21**. This section exists only as an integration boundary for the agentic patterns in sections 17-33.

- Agent panels become drawers below 1280px and full-screen task views below 768px.
- Review cockpits preserve evidence, proposal, human decision, and audit order when columns collapse.
- Queue density follows the global compact, standard, and comfortable tokens; individual modules must not invent row heights.
- Consequential mobile actions remain unavailable until the complete evidence and approval flow passes role-based mobile usability and accessibility validation.
- Responsive behavior must preserve context, drafts, selections, filters, and recovery paths; reflow must never silently discard work.

---

# 36. Frontend Implementation Alignment

The canonical implementation contract is **Visual Design System section 24**. For IIP, the inspected portal baseline is Next.js 15, React 19, Tailwind CSS 3.4, Recharts, Lucide, Storybook, Playwright, Testing Library, and axe-core.

Do not treat an aspirational library list as an installed stack. Radix UI, TanStack Table, React Hook Form, Zod, D3, or another design-system dependency may be introduced only through an approved architecture decision, an identified component gap, bundle and accessibility review, and an incremental migration plan.

CSS custom properties are the runtime semantic-token interface. Tailwind aliases consume those properties. Components consume semantic or component tokens rather than primitives or hard-coded values.

---

# 37. Design Tool and Token Delivery

The design library and code must share the semantic token names, modes, density variants, and component states defined in **Visual Design System sections 3, 22, and 24**. The DTCG-compatible token source is the exchange contract; CSS custom properties are the runtime contract; Figma variables and Tailwind aliases are generated or manually reconciled consumers until automation is approved.

The design library must include foundations, light/dark modes, density modes, shell, navigation, tables, forms, overlays, feedback, AI provenance, evidence, agent task, review, approval, audit, and domain-extension components. Every component page includes anatomy, dimensions, variants, states, keyboard behavior, accessibility notes, content rules, and implementation mapping. A release is incomplete if design and code use different token names or omit error, restricted, stale, not-configured, and permission-denied states.

---
