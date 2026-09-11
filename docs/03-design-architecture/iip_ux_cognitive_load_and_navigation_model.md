# IIP UX Cognitive Load and Navigation Model

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-09-08 |
| Status | **Approved interaction direction.** Owns the shell navigation model, the chat-first landing, progressive disclosure, and the attention/interruption policy for `web/portal`. |
| Applies to | The authenticated staff portal `/app/*`. |
| Relationship to existing IA | `iip_information_architecture_navigation_and_screen_inventory.md` continues to own the canonical object model, route IDs, route-to-label map, and permission-derived visibility. **This document supersedes its navigation-layer and shell-chrome content**: the grouped multi-section sidebar it describes is replaced by the model in §5 here. Where the two conflict on chrome, this document wins; where they conflict on a route's existence or authority, that document and current code win. |
| Companion documents | `iip_design_language_v2.md` (how it looks), `iip_workflow_legibility_contract.md` (what state must be visible), `iip_ui_craft_guardrails.md` (craft floor) |

---

## 1. The problem this document solves

The portal today exposes roughly twenty destinations across six sidebar groups, plus a separate admin tree. Every one of them is a decision the user must make before doing any work. A claims officer who opens the product does not want to choose between "My Work", "Claims", "Channels", "Work Items", and "Command Center" — those distinctions are ours, not theirs. They want to know what needs them, and get on with it.

The target: **a user achieves their goal without training, without a manual, and without first learning our information architecture.**

## 2. The cognitive-load model we design against

Cognitive load theory splits mental effort into three parts. Only one of them is ours to spend.

| Type | What it is here | Our obligation |
|---|---|---|
| **Intrinsic** | The genuine difficulty of the insurance work — assessing whether a repair estimate is consistent with a police abstract. | Preserve it. This is the expert's job and the reason they are employed. Never dumb it down or hide the evidence behind it. |
| **Extraneous** | Effort spent on *the software* rather than the work — finding the screen, decoding a label, remembering a claim reference between two pages, working out which of five buttons is the real one. | **Drive to zero.** Every rule in this document exists to remove extraneous load. |
| **Germane** | Effort that builds a durable mental model of how the product works. | Spend it once, early, on one consistent model — then never change the model. |

The practical test for any proposed screen: *does this add intrinsic load (good), or extraneous load (unacceptable)?*

## 3. Principles, and the concrete rule each one produces

These are established interaction-design principles. Each is followed by the binding rule it generates in this product — implement the rule, not your interpretation of the principle.

| Principle | Rule in IIP |
|---|---|
| **Hick's Law** — decision time grows with the number and complexity of choices. | The rail groups destinations under named headings rather than presenting one long undifferentiated list (§5.2, amended 2026-09-11) — grouping, not a hard destination ceiling, is what keeps a longer list decidable. |
| **Recognition over recall** (Nielsen heuristic 6) | The user never needs to remember an identifier, a filter, or a state between screens. Context travels with them: opening a claim from chat carries the claim reference, the question that led there, and the filter that produced it. |
| **Visibility of system status** (heuristic 1) | Every surface answers "what is happening and what is the system doing" without the user asking. Every asynchronous action reports its outcome. No silent failures, no silent successes. |
| **Match between system and the real world** (heuristic 2) | Labels use the insurer's vocabulary, not ours. "Claim", "policy", "assessor", "police abstract" — never "item", "entity", "record", "object", "resource". |
| **Jakob's Law** — users expect your product to work like the others they use. | Chat behaves like every chat they know. Tables sort by clicking the header. `Ctrl/Cmd+K` opens search. Breadcrumbs go up. Do not invent novel interactions for solved problems. |
| **Tesler's Law of conservation of complexity** — irreducible complexity moves, it does not vanish. | Where complexity cannot be removed, the *system* absorbs it, not the user. The agent triages, classifies, and scores; the human reviews a prepared decision rather than assembling one. |
| **Fitts's Law** — acquisition time depends on target size and distance. | Primary actions are at least `36px` tall and placed where the eye already is — at the end of the content the user just read, not in a distant corner. |
| **Progressive disclosure** | Each screen shows what is needed to decide. Supporting evidence opens in a side drawer without leaving the decision. Configuration lives in Settings, never inline in a work surface. |
| **Doherty threshold** — interaction stays engaging below ~400ms. | Any action expected to exceed 400ms shows immediate acknowledgement. Below 400ms, show nothing rather than flashing a skeleton. Optimistic UI is permitted **only** for reversible, non-consequential actions — never for an approval, a send, or a write-back. |
| **Miller's limit, applied honestly** | The "7±2" figure concerns short-term memory, not list length, and does not mean menus must have seven items. Use it correctly: never require the user to *hold* more than a few facts in their head at once. Tables may be long; the number of things the user must remember must not be. |
| **Aesthetic-usability effect** | A clean interface is trusted more and its faults forgiven — which is precisely why craft is a safety issue here, not a preference. See `iip_ui_craft_guardrails.md`. |

## 4. The five questions every screen answers on sight

Before a screen is considered designed, a first-time user must be able to answer all five in under five seconds, from the layout alone, with no tooltip and no documentation:

1. **Where am I?** — breadcrumb, active rail item, page title.
2. **What is this?** — one-line description under the `h1`, in the insurer's language.
3. **What needs me?** — the count of items awaiting this user, above the fold, clickable.
4. **What do I do next?** — exactly one visually primary action.
5. **How do I get back?** — breadcrumb or an unambiguous close.

If a screen cannot answer one of these, the screen is wrong. Do not solve it with a tooltip, a help link, an onboarding tour, or an empty-state paragraph explaining the product.

---

## 5. Navigation model

### 5.1 Three tiers, always

```
┌──────┬──────────────────┬──────────────────────────────────────────┐
│ 64px │  contextual      │  content column                          │
│ rail │  panel 256px     │  max 960px / 1040px, left-aligned        │
│      │  (screen-        │                                          │
│      │  dependent;      │  breadcrumb                              │
│      │  absent on some) │  h1 + primary action                     │
│      │                  │  one-line description                    │
│      │                  │  content                                 │
└──────┴──────────────────┴──────────────────────────────────────────┘
```

Geometry is specified in `iip_design_language_v2.md` §3. The rail is always present; 64px is its collapsed width, and it expands to roughly 240px per §5.2's amendment below. The contextual panel appears only where a screen has genuine sub-navigation or history. The content column never centres itself in the viewport.

### 5.2 The rail destinations (amended 2026-09-11)

**This section originally specified a flat, exhaustive list of seven rail destinations, with every other real screen nested as a tab inside one of the seven (§5.3, as it read before this amendment).** That nesting was never built: Customers, Communications, Knowledge, Acquisition, Policy, Fraud, Underwriting, and Audit all shipped as full, real, permission-gated top-level pages — not tabs — and stayed that way. The result was a rail that only ever pointed at seven of roughly fifteen real destinations, with no link anywhere in the product to the rest; verified before this amendment by grepping every `.tsx` file in `web/portal` for a link to each orphaned route and finding none. This is what "some icons cannot be reached" and "I found it hard to know where to find it" — a working platform's own author, about his own claims queue — actually meant in practice, per the readiness blueprint (`docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md` §10).

Two ways to close that gap were on the table: build the original tabs-in-seven plan for real (the larger, arguably more disciplined fix — collapsing Customers/Communications/Knowledge/Acquisition/Policy/Fraud/Underwriting/Audit into tabs inside Claims, Inbox, Automation and Insights, with per-tab URL state per §5.5), or accept that these are load-bearing top-level screens today and give the rail room for them. This amendment takes the second path, because the first is a substantially larger, riskier change (new tab scaffolding across eight screens, migrating each one's state into query strings) that the platform's other gaps did not leave room for in this pass. **The first path remains open** — nothing here forecloses eventually building the tabs-in-seven design properly; this amendment records the interim decision and its reasoning so that choice is made deliberately later, not rediscovered.

The rail is now **expandable** rather than fixed-width: collapsed, it is the original 64px icon-only rail; expanded (the default for a new user, persisted per-browser thereafter), it is roughly 240px wide with a text label beside every icon, and destinations are organised into named groups so the list stays scannable rather than one long undifferentiated column. Hick's Law (§2) still governs — grouping is exactly the technique for keeping a longer list decidable — but the "seven, and no more" ceiling is retired.

| Group | Destination | Route | Icon (lucide) | What it holds |
|---|---|---|---|---|
| Work | **Assistant** | `/app` | `message-square` | The default landing surface. Conversation with the agents, scoped to this user's tenant and permissions. |
| Work | **My work** | `/app/work-items` | `list-checks` | Everything awaiting *this user*, across every workflow. The single queue. |
| Work | **Claims** | `/app/claims` | `file-search` | Every claim in the tenant, filterable. The system of record view. |
| Work | **Inbox** | `/app/channels` | `inbox` | Inbound customer contact — email, WhatsApp — before it becomes a claim. |
| Customers | **Customers** | `/app/customers` | `users` | The customer record: policies, contact history, cases. |
| Customers | **Acquisition** | `/app/acquisition` | `building-2` | Leads inbox — manual capture today; channel ingestion lands here once built (blueprint §8). |
| Customers | **Communications** | `/app/communications` | `mail` | Every outbound customer message, across channels, with delivery status. |
| Intelligence | **Documents** | `/app/documents` | `file-text` | Document intelligence: extraction, classification, review. |
| Intelligence | **Knowledge** | `/app/knowledge` | `book-open` | Approved sources for grounded AI answers — upload, classify, approve. |
| Intelligence | **Policy** | `/app/policy` | `scroll-text` | AI-prepared policy/coverage guidance, grounded and cited. |
| Intelligence | **Fraud** | `/app/fraud` | `shield-alert` | Advisory fraud signals — never a determination. |
| Intelligence | **Underwriting** | `/app/underwriting` | `gavel` | AI-prepared underwriting signals for human review. |
| Operations | **Automation** | `/app/flows` | `git-branch` | Agents and flows: what runs, what it did, what it escalated. |
| Operations | **Insights** | `/app/executive` | `line-chart` | Volume, resolution rate, handle time, exceptions, SLA. |
| Operations | **Audit** | `/app/audit` | `shield-check` | The audit trail: every consequential action, its reason, its reviewer. |

Pinned to the bottom of the rail: the **user avatar** only. Its menu carries **Search** (`Ctrl/Cmd+K` also opens it directly), **Settings**, **Integrations**, **Support**, **Sign out**, and — for permitted roles only — **Admin console**. Settings and Search were previously separate pinned icons alongside the avatar; collapsing them into one pinned control is itself part of this amendment; see §5.4a.

### 5.3 Where the other surfaces went

Nothing is deleted. Command Center became **Assistant** (unchanged by this amendment). Admin console and its children (Users, Agents, Billing, Onboarding, Tenant Policy, WhatsApp, Email, Connections) stay behind the avatar menu — administration is not daily work and must not occupy daily-work rail space, exactly as originally specified.

### 5.4 Rail behaviour

- Active state: `background --color-primary-soft`, glyph (and label, when expanded) `--color-primary`.
- Hover: `background --color-bg-page`, glyph `--color-text-secondary`.
- Collapsed, every rail button has a tooltip on hover/focus and an `aria-label` — icon-only navigation remains acceptable there because the set per group is small and learned within a session. Expanded, the visible label is the primary label and the tooltip is redundant.
- A destination with items awaiting this user shows a count badge: a `16px` pill at the top-right of the button, `--color-danger-dot` background, white `10px/700` numeral, capped at `9+`.
- A destination the user's permissions exclude is **not rendered**. Never render a disabled destination the user can never use.

### 5.4a Expand/collapse and the pinned-region fix

A second, independent defect the same investigation surfaced: the rail split into a pinned top (logo, workspace badge), one scrollable middle (destinations), and a pinned bottom (role switcher, Settings, Search, avatar) — and on a short viewport the pinned regions could squeeze the scrollable middle below the height of its own content, with no visible scrollbar affordance on a 64px-wide column to signal that anything was clipped. Fixed by: reducing the pinned bottom to the avatar alone (Settings and Search moved into its menu, as above); adding a top/bottom clipping-fade that only renders when the destination list is actually taller than its container; and letting the destination list's scroll region take priority over the pinned chrome around it. Verify this at **768px height**, not only 768px width — the defect does not reproduce on a narrow-but-tall viewport.

Expand/collapse state persists per browser (`localStorage`, key `iip.sidebar.expanded`) and defaults to expanded for a browser that has never set it — collapsed is an expert affordance a user opts into, not a default that hides the product's own navigation on day one.

### 5.5 Within-screen navigation

Tabs, not new destinations. Tabs use the filter-pill treatment from the design language §8.11, carry counts, and preserve their selection in the URL so a screen can be linked, bookmarked, and shared. Every filter and tab state must be reflected in the query string — a colleague pasting a link must see exactly what the sender saw.

---

## 6. The chat-first landing

### 6.1 Why chat is the front door

The user's first screen is the Assistant at `/app`. The reasoning is the same as a coding agent's: enormous capability underneath, one text field on top. A claims officer should not have to know which of seven screens holds the answer to "what's stuck?" — they should be able to ask.

Chat is the **front door, not the whole house.** It answers, and then it hands off into the real surfaces where work is done. A chat that can only talk is a demo. See §7.

### 6.2 The empty state

Exactly four elements, vertically centred, and nothing else: the orb, a greeting, a subhead, and up to three suggestion chips.

- **Greeting:** `Good morning, {first name},` — time-of-day resolved in the user's timezone. Comma, not exclamation mark.
- **Subhead:** `How can I help with your work today?`
- **Suggestion chips:** generated from this user's actual state at page load, never hardcoded. Resolve in this priority order and take the first three that apply:

| Priority | Condition | Chip text |
|---:|---|---|
| 1 | User has items in `RequiresReview` | `Show my {n} items awaiting review` |
| 2 | User has a claim with an SLA at risk or breached | `Which of my claims are at risk of breaching SLA?` |
| 3 | Tenant has claims in `MissingDocuments` | `Which claims are waiting on documents?` |
| 4 | Automation escalated something in the last 24h | `What did the agents escalate overnight?` |
| 5 | Fallback, new or empty tenant | `What can I ask you about?` |

If the user has no work at all, say so plainly — "You have nothing awaiting review." — rather than showing chips that lead to empty lists. An empty product that pretends to be busy is worse than one that is honestly quiet.

### 6.3 What the Assistant may and may not do

Governed by `AGENTS.md` invariants and `iip_workflow_legibility_contract.md` §9. In summary: it may summarise, locate, explain, extract, draft, and navigate. It may not approve, reject, settle, price, bind, allege fraud, or send a regulated communication. When asked to do one of those, it does not refuse blankly — it prepares the work and hands the user the human decision surface, saying which authority is required and why.

Every factual assertion carries a citation chip (design language §8.20). An assertion that cannot be cited is not rendered.

---

## 7. The chat-to-work handoff (mandatory)

This is the contract that stops the Assistant from being a novelty.

When a reply concerns specific objects, it ends with **action cards** (design language §8.21), not with prose containing a link. Rules:

1. One card per object, maximum five, then "View all {n}" as a text link.
2. Each card shows: object title, its status pill, two or three decision-relevant facts (SLA, completeness, owner), and one primary button carrying the verb the user would use — `Review claim`, `Check documents`, `Open work item`.
3. Clicking through carries context: the originating conversation id, the filter that produced the set, and the question asked. The destination screen shows a dismissible context line at the top — `From your question: "which claims are waiting on documents?"` — with a back link to the conversation.
4. The reverse direction also works: any work surface has an "Ask about this" affordance that opens the Assistant pre-scoped to that object.

A user must never have to copy a claim reference out of chat and paste it into a search box. If they do, the handoff has failed.

---

## 8. Attention and interruption policy

Every interruption spends the user's attention. This section says exactly what may spend it. **This policy is binding**; a surface may not invent its own notification behaviour.

### 8.1 The four channels, ranked by cost

| Channel | Cost | Use for | Never use for |
|---|---|---|---|
| **Inline, in place** | Lowest | The result of an action the user just took, shown where they took it. Validation, save confirmation, a state change on the row they touched. | Anything that happened elsewhere. |
| **Count badge** (rail, tab, stat tile) | Low | Ambient "there is more work" signalling. Always accumulating, never interrupting. | Anything urgent enough to need reading now. |
| **Toast** | Medium | Something changed *elsewhere* that this user must know about now, and can act on in one click. | Routine success of the user's own action (that is inline); anything they cannot act on; anything that will still be true in an hour. |
| **Modal** | Highest | Confirming a consequential or irreversible action, and nothing else. | Announcements, tips, marketing, onboarding, "what's new". |

### 8.2 Toast rules

A toast is permitted only when **all four** are true:

1. The event happened outside the user's current screen.
2. It concerns work assigned to, or owned by, this user.
3. It is actionable now.
4. It has not already been shown to this user in this session.

Events that qualify:

| Event | Toast |
|---|---|
| A claim triaged by an agent lands in this user's queue in `RequiresReview` | `Claim CLM-2026-0184 needs your review` · detail: `Motor own damage · completeness 71%` · action: `Review` |
| A claim owned by this user moves to SLA `Breached` | `CLM-2026-0177 has breached SLA` · action: `Open` |
| An approval this user requested is decided | `Your escalation on CLM-2026-0165 was approved by A. Nakato` · action: `Open` |
| A document this user uploaded finishes processing with findings needing review | `Extraction complete — 2 fields need checking` · action: `Review` |
| A previously working provider becomes unavailable and blocks this user's task | `Document extraction is unavailable` · detail names the provider owner · no action |

Events that must **never** toast: another user's work; any agent activity that resolved cleanly with no human need; bulk or batch progress; anything already visible as a badge; system metrics; sign-in; page loads; "welcome back".

### 8.3 Rate limiting

At most **three** toasts visible at once and at most **five per minute**; beyond that, collapse into a single `{n} more updates` toast that opens the notification panel. Deduplicate by object id — a claim that changes state three times produces one toast showing the latest state, not three.

### 8.4 The notification bell

Top-right of the content column. Holds the durable record of everything that toasted plus everything that was rate-limited. Grouped by day, unread marked with a `6px` violet dot, "Mark all read" available. The badge count equals unread items. This is the safety net that lets toasts be rare: nothing is lost by not toasting.

---

## 9. Search and the command palette

`Ctrl/Cmd+K` from anywhere. A single input that resolves, in this order: claim references, customer names and phone numbers, policy numbers, documents, then navigation destinations. Results are grouped with `11px/700` uppercase group labels, keyboard-navigable with arrows, `Enter` opens, `Escape` closes.

Search is scoped server-side to the user's tenant and permissions — never filtered client-side. A result the user may not open must not appear.

---

## 10. Progressive disclosure rules

| Situation | Rule |
|---|---|
| Supporting evidence for a decision | Side drawer. The decision stays visible behind it. Never a full page navigation, never a modal. |
| A long list of extracted fields | Show fields below confidence threshold expanded; collapse the clean ones behind `{n} fields verified` which expands in place. |
| Configuration | Settings only. Never inline in a work surface. |
| Advanced filters | One row of the three most-used filters, plus "More filters" opening a popover. Applied filters always render as removable chips. |
| Destructive or irreversible action | Confirmation modal naming the specific object and the specific consequence. Never a bare "Are you sure?". |
| An explanation longer than two sentences | It belongs in the object, not in a tooltip. Tooltips name things; they do not teach. |

---

## 11. Forms

- One column. Never two side-by-side columns of fields — the eye loses the thread and tab order becomes ambiguous.
- Group into cards of at most six fields, each card with a title.
- Label above the field, always visible. Placeholder text is an example, never a label.
- Validate on blur, not on every keystroke. Show errors below the field, in text, with the field's border in `--color-danger-text`.
- On submit failure, focus the first invalid field and summarise at the top of the form.
- Never lose input. Navigating away from a dirty form prompts; a failed save preserves everything the user typed.
- Required fields are marked; optional ones are not. If most fields are required, mark the optional ones instead and say so.

---

## 12. The no-manual test

Before any new surface merges, run this test and record the result:

1. Take a person who knows insurance operations but has never seen IIP.
2. Give them one goal in their own words — "find out which claims are waiting on documents and chase one".
3. Give them no explanation, no tour, no documentation.
4. Watch. Do not help.

Pass criteria: they reach the goal without asking a question, without opening the wrong destination more than once, and without needing to be told what a label means. Any hesitation longer than five seconds marks a design defect at that point — record it and fix the screen, not the person.

Until real participants are available, an implementing agent must at minimum perform this walkthrough itself, in the browser, narrating each decision point and recording every hesitation. A surface that has never been walked end to end has not been designed.

---

## 13. Accessibility is cognitive load

These are not a separate compliance workstream; they are the same problem.

- Focus order follows visual order. Always.
- Visible focus ring on everything focusable, meeting 3:1 against its background.
- Every status change announced in a live region — a screen-reader user must not have to poll the page.
- All content and function reachable by keyboard alone; no keyboard trap other than deliberate modal focus traps that release on `Escape`.
- Reflow to 320 CSS px without horizontal scroll or loss of function.
- Text resizable to 200% without clipping.
- Motion honours `prefers-reduced-motion`.
- Non-colour encoding for every state — the dot-plus-label pill pattern exists for exactly this reason.

---

## 14. Acceptance criteria

A navigation or interaction change is done when:

1. Every real, permission-gated screen has a rail destination or is one click from one, grouped sensibly (§5.2, amended 2026-09-11), and every destination is reachable and permission-correct.
2. `/app` renders the Assistant with dynamically generated chips derived from this user's real state.
3. Every assistant reply concerning objects ends in action cards that carry context on click.
4. Every toast raised satisfies all four conditions in §8.2, and the bell holds everything.
5. Every filter and tab selection round-trips through the URL.
6. The five questions in §4 are answerable on every new screen.
7. The §12 walkthrough was actually performed, with hesitations recorded.
8. Keyboard and screen-reader paths were exercised, not assumed.
