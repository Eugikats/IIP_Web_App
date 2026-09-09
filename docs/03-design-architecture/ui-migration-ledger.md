# UI migration ledger — design language v2

Tracks screen-by-screen migration of `web/portal` onto the design language v2
foundation (`iip_design_language_v2.md`). One row per screen. A row is marked
**Done** only after it has been opened in a browser, verified against the
27-point checklist in `iip_ui_craft_guardrails.md` §8, and committed.

Resume rule: find the first row not marked Done and migrate that screen next.
Never have two screens in flight.

| # | Tier | Route | Component | Status | Date | Commit |
|---:|---|---|---|---|---|---|
| 1 | 1 | `/app` | `lovable-pages/AIConversations.tsx` | Done | 2026-09-08 | `d493670` |
| 2 | 1 | `/app/work-items` | `lovable-pages/ReviewWorkspace.tsx` (+ UniversalMyWork, AgentWorkItems, Workflows) | Done | 2026-09-08 | `364b45b` |
| 3 | 1 | `/app/claims` | `lovable-pages/ClaimsQueue.tsx` | Done | 2026-09-08 | `083a07e` |
| 4 | 1 | `/app/claims/[id]` | `lovable-pages/ClaimReview.tsx` | Done | 2026-09-08 | `c272afc` |
| 5 | 1 | `/app/claims/[id]/extraction` | `lovable-pages/DocumentExtraction.tsx` | Done | 2026-09-08 | `cd31e91` |
| 6 | 1 | `/app/documents` | `lovable-pages/Documents.tsx` | Done | 2026-09-09 | `949205d` |
| 7 | 1 | `/app/channels` | `lovable-pages/Channels.tsx` | Done | 2026-09-09 | `04704f5` |
| 8 | 2 | `/app/conversations` | `lovable-pages/RecentConversations.tsx` | Not started | | |
| 9 | 2 | `/app/claims/new` | `lovable-pages/FNOL.tsx` | Not started | | |
| 10 | 2 | `/app/customers` | `lovable-pages/Customers.tsx` | Not started | | |
| 11 | 2 | `/app/customers/[id]` | `lovable-pages/CustomerDetail.tsx` | Not started | | |
| 12 | 2 | `/app/communications` | `lovable-pages/Communications.tsx` | Not started | | |
| 13 | 2 | `/app/knowledge` | `lovable-pages/Knowledge.tsx` | Not started | | |
| 14 | 2 | `/app/search` | `lovable-pages/GlobalSearch.tsx` | Not started | | |
| 15 | 3 | `/app/underwriting` | `lovable-pages/Underwriting.tsx` | Not started | | |
| 16 | 3 | `/app/policy` | `lovable-pages/PolicyIntelligence.tsx` | Not started | | |
| 17 | 3 | `/app/fraud` | `lovable-pages/FraudIntelligence.tsx` | Not started | | |
| 18 | 3 | `/app/acquisition` | `lovable-pages/CustomerAcquisition.tsx` | Not started | | |
| 19 | 4 | `/app/flows` | `lovable-pages/AgentFlows.tsx` | Not started | | |
| 20 | 4 | `/app/flows/[key]` | `lovable-pages/AgentFlowInspector.tsx` | Not started | | |
| 21 | 4 | `/app/flows/new` | `lovable-pages/AgentFlowBuilder.tsx` | Not started | | |
| 22 | 4 | `/app/runs/[id]` | `lovable-pages/AgentRunDebugger.tsx` | Not started | | |
| 23 | 4 | `/app/executive` | `lovable-pages/Executive.tsx` | Not started | | |
| 24 | 4 | `/app/audit` | `lovable-pages/Audit.tsx` | Not started | | |
| 25 | 5 | `/app/settings` | `lovable-pages/Settings.tsx` | Not started | | |
| 26 | 5 | `/app/integrations` | `lovable-pages/Integrations.tsx` | Not started | | |
| 27 | 5 | `/app/admin` | `lovable-pages/Admin.tsx` | Not started | | |
| 28 | 5 | `/app/admin/users` | `lovable-pages/admin/Users.tsx` | Not started | | |
| 29 | 5 | `/app/admin/agents` | `lovable-pages/admin/Agents.tsx` | Not started | | |
| 30 | 5 | `/app/admin/tenant-policy` | `lovable-pages/admin/TenantPolicy.tsx` | Not started | | |
| 31 | 5 | `/app/admin/onboarding` | `lovable-pages/admin/Onboarding.tsx` | Not started | | |
| 32 | 5 | `/app/admin/email` | `lovable-pages/admin/EmailInbox.tsx` | Not started | | |
| 33 | 5 | `/app/admin/whatsapp` | `lovable-pages/admin/WhatsAppSettings.tsx` | Not started | | |
| 34 | 5 | `/app/admin/billing` | `lovable-pages/admin/Billing.tsx` | Not started | | |
| 35 | 5 | `/app/support` | `app/app/support/page.tsx` | Not started | | |
| 36 | 6 | `/access` | `lovable-pages/ClaimAccessRequest.tsx` | Not started | | |
| 37 | 6 | `/access/verify` | `lovable-pages/ClaimAccessVerify.tsx` | Not started | | |
| 38 | 6 | `/access/claim` | `lovable-pages/ClaimAccessStatus.tsx` | Not started | | |
| 39 | 6 | `/access/new-claim` | `lovable-pages/ClaimAccessNewClaim.tsx` | Not started | | |

## Foundation slice

Committed `026e576` on `harry/ui-design-language-foundation` — tokens
(`app/globals.css`), shell (`components/iip/AppShell.tsx`, `Sidebar.tsx`),
primitives (`components/ui/`). Screens 1–39 build on this; do not modify the
shell or primitives from a screen-migration run — stop and report instead.

Two more foundation-level shared dependencies were found and migrated after
this initial commit (both used by many screens or the shell, not one screen
— see the run log for each): the chat surface (`ChatPrimitives.tsx`,
`CommandCenterAnswer.tsx`, `CopilotPanel.tsx`, `DataProvenance.tsx`, commit
`d493670`) and `components/iip/Primitives.tsx` (`GlassCard`, `SectionHeader`,
`Badge`/`StatusBadge`/`SLABadge`/`RiskBadge`/`ConfidenceBadge`, commit
`5b7c2e4` — imported by 38 of 39 screens). Check whether a bespoke element a
screen wants to replace already has a migrated equivalent in one of these
two files, `components/Badges.tsx`/`MetricCard.tsx`/`SectionCard.tsx`/
`DataTable.tsx`/`PageHeader.tsx`/`States.tsx`, or `components/ui/`, before
writing new markup for it.

## Run log

- 2026-09-08: ledger created; foundation-slice commit `026e576`.
- 2026-09-08: screen 1 (`/app`) — commit `d0a12d6`. Migrated the screen's
  self-contained markup (empty-state hero, suggestion chips, history/grounding
  banners, composer wrapper) onto `dls` tokens and `components/ui/`
  primitives. Verified: `pnpm lint`, `pnpm exec tsc --noEmit`,
  `node scripts/check-ui-release-gates.mjs`, `pnpm test` (all suites) and
  `pnpm build` (compiles and generates all pages; fails only at the
  pre-existing Windows `output: "standalone"` trace-copy EPERM step, proven
  pre-existing against `main` in the foundation-slice run) all pass. Opened in
  the Browser pane at 1280px, 768px, and 320px — no horizontal scroll, focus
  ring on the new chip verified via `getComputedStyle`/`:focus-visible`
  (`outline: 2px solid`, `box-shadow: none`).

  **Not marked Done — three unresolved items, all presentation-only-scope
  limits rather than defects:**
  1. **Shared chat surface left unmigrated.** `ChatPrimitives.tsx`
     (`AIMessageBubble`, `UserMessageBubble`, `Composer`) and
     `CommandCenterAnswer.tsx` (`CommandCenterBlocks`) are also imported by
     `components/iip/CopilotPanel.tsx`, which `AppShell.tsx` renders on every
     screen. Migrating them to satisfy this screen would silently restyle
     shell chrome — an explicit DO-NOT for a screen-migration run. Message
     bubbles, the composer, and every AI-answer block type still render with
     pre-token classes (`glass-elevated`, `gradient-primary`, bespoke
     per-block colours) until a deliberate decision is made to migrate this
     shared surface (recommend treating it like the foundation slice: one
     dedicated commit, its own review, before continuing further screens —
     it is used by the shell, not just this screen, so it sits above the
     39-screen worklist rather than inside it).
  2. **Suggestion chips only partially satisfy nav-model §6.2.** The
     priority-order table needs this user's own `RequiresReview` items
     (priority 1), this user's own at-risk claims (priority 2), and
     automation escalations in the last 24h (priority 4). No hook already
     loaded on this screen exposes user-scoped work-item state or a
     time-windowed escalation count — `useOperationsSummary` is a tenant-wide
     aggregate. Adding a new query is out of scope for a presentation-only
     migration (explicit DO-NOT). Implemented: priority 3 (tenant claims
     missing documents, from the real `incompleteClaims` field) and the
     priority-5 fallback. Follow-up: either accept this reduced chip set for
     `/app`, or scope a small backend addition (a user-scoped work-item
     summary endpoint) as its own task.
  3. **§8.24 six states: three apply, three don't, by the screen's own data
     model.** Loading, error, and empty-no-data are implemented and were
     visually verified. Empty-after-filter doesn't apply (this screen has no
     filters). Not-configured is handled per-answer inline by the (untouched)
     `not_configured` answer block, not as a whole-screen state. No-permission
     is handled by the shell's route gate before this screen renders. Craft
     guardrails checklist items 14/15 are therefore an honest "partial", not
     a clean yes — recorded here rather than forced green.

  Visual-regression snapshots (`gate-10-ai-conversations`,
  `gate-11-command-center`, `gate-16-conversation-continuity`) were **not**
  re-baselined this run: Playwright's Chromium binary is not installed on
  this machine and this environment has no network access to fetch it
  (`pnpm exec playwright install chromium` fails with `ENOTFOUND
  cdn.playwright.dev`). This blocks the whole visual-regression suite,
  unrelated to this screen's changes — needs either network access or a
  pre-provisioned browser cache before it can run here.

  A stale `node` process (PID 24332, running since 2026-09-01) was holding
  `web/portal/.next-dev.lock` and blocking any dev/test server from starting.
  Stopped with explicit user approval; the lock file was also removed.

- 2026-09-08: shared chat surface — commit `d493670`. Resolves open item 1
  above: `ChatPrimitives.tsx`, `CommandCenterAnswer.tsx`, `CopilotPanel.tsx`,
  and `DataProvenance.tsx` migrated onto design language v2 §8.17 (chat
  surface) exactly, treated as a foundation-level shared dependency (used by
  the shell's `CopilotPanel`, not just one screen) rather than screen-1-owned
  code — same precedent as the original foundation slice migrating
  Badges/MetricCard/etc. External component signatures unchanged. Found and
  fixed a real pre-existing vocabulary-contract violation in the process:
  claim/approval status fields were rendering the raw backend enum name
  (e.g. literally `MissingDocuments`) instead of the §10.1/§10.3 label with
  the correct dot tone — added the lookup tables and StatusPill rendering.
  Added one missing token, `--color-primary-idle` (`#E4E1F7`), that §8.17
  specifies for the composer's idle send button but the foundation slice had
  not yet named. Removed four confirmed-dead ChatPrimitives exports
  (`InsightCard`, `ApprovalCard`, `ChannelBadge`, `PriorityBadge` — the last
  of which rendered priority as a coloured pill with a non-vocabulary
  "Medium" value, doubly non-compliant with §10.5). Verified: `pnpm lint`,
  `pnpm exec tsc --noEmit`, `check-ui-release-gates.mjs`, `pnpm test` (all
  suites), `pnpm build` (compiles; same pre-existing EPERM trace-copy step)
  all pass. Browser-verified via `NEXT_PUBLIC_VISUAL_PARITY_MODE=true` at
  1280px and 768px: bubble shapes/colours match §8.17 exactly per
  `getComputedStyle`, send-button idle/active colour confirmed, focus rings
  confirmed on previously-unstyled CopilotPanel controls (Hide, Expand,
  starters). Not click-through-verified live: `claims_table`, `sla_summary`,
  `approval_queue`, `action_proposal`, `draft_communication`,
  `not_configured`, `error` block renderers — the parity-mode synthetic
  answer builder returns the same fixed blocks regardless of question, and
  the real backend isn't reachable here. These are typecheck-clean and reuse
  primitives (StatusPill, Card-style surfaces) already visually confirmed by
  other blocks in this same change, but were not individually eyeballed.

  **Genuinely missing primitive, not fixed — flagging per the stop-and-report
  rule rather than inventing one:** design language §8.16 (side drawer) has
  no `components/ui/` implementation yet. §8.20 requires an evidence citation
  chip to open its source in that drawer; without the primitive, the
  `evidence_list` block renders citation chips matching §8.20's visual spec
  exactly but non-interactive (no click target — `evidence.ref` is an
  internal reference id, not a URL, so there is nothing honest to link it to
  either). Whichever screen first needs real evidence-citation click-through
  (tier 1's `ClaimReview.tsx` almost certainly will) will need this primitive
  built first — recommend building it then, not speculatively now.

  Screen 1 (`/app`) is now marked **Done**. The two remaining items from the
  prior entry (suggestion chips only covering priorities 3/5 of nav-model
  §6.2; three of six §8.24 states genuinely inapplicable to a chat screen)
  are accepted, permanent, documented scope limits of a presentation-only
  migration — not defects blocking completion. Resuming at screen 2
  (`/app/work-items`).

- 2026-09-08: foundation-level fix — commit `5998df5`. Found while starting
  screen 2: `components/iip/Primitives.tsx` (`GlassCard`, `SectionHeader`,
  `Badge`/`StatusBadge`/`SLABadge`/`RiskBadge`/`ConfidenceBadge`) is imported
  by **38 of the 39 screens** on this ledger — more than any other file, and
  missed by the original foundation slice, which migrated a different,
  similarly-named `components/Badges.tsx` instead. Migrated in its own commit
  `5b7c2e4` per the same precedent as the chat surface: external signatures
  unchanged, every caller keeps compiling untouched. Found and fixed real
  vocabulary-contract violations while there (raw enum names rendered
  verbatim as labels; three status/tone pairings that contradicted
  §10.1-10.2 outright — see that commit for the full list). Also added
  `components/ui/Input.tsx`'s new `Select` (native `<select>` styled to the
  same control contract as `Input` — the radius token table already groups
  them, not a new component) and migrated `GuardrailBanner.tsx` (19 callers)
  in commit `5998df5`. **Check whether a bespoke element already has a
  migrated equivalent in one of these shared files before writing new markup
  for it** — this is now noted in the Foundation slice section above.

- 2026-09-08: screen 2 (`/app/work-items`) — commit `364b45b`. Migrated
  `ReviewWorkspace.tsx`, `UniversalMyWork.tsx`, `AgentWorkItems.tsx`, and
  `Workflows.tsx` (all four files this ledger row covers) onto design
  language v2 tokens and primitives, building on the two foundation fixes
  above. Segmented tab/view switchers rebuilt on tokens (previously a black
  `bg-ink` active pill — not a status or brand colour). Filters moved onto
  `Field`/`Select`. Loading/error/empty states moved onto
  `components/ui/StateSurfaces`, and empty-after-filter is now visually
  distinct from empty-no-data in `UniversalMyWork` (previously identical).
  Consequential actions (accept, escalate, return for correction, approve,
  execute, reject, override) kept their existing reason-textarea gate
  unchanged; Approve/Accept use `Button`'s primary variant (flat violet, the
  old `gradient-primary` class is gone); Reject uses the destructive
  variant's lower-emphasis, text-only treatment per that primitive's own
  contract. `Workflows.tsx`'s dense approval table keeps hand-styled compact
  action buttons on tokens rather than the full `Button` primitive, matching
  craft guardrails' density guidance for a table staff scan continuously.

  Verified: `pnpm lint`, `pnpm exec tsc --noEmit` (clean across all 38
  `Primitives.tsx` consumers), `check-ui-release-gates.mjs`, `pnpm test` (all
  suites), `pnpm build` (compiles; same pre-existing EPERM trace-copy step)
  all pass. Browser-verified at 1280px and 768px across all three
  `/app/work-items` tabs: active-tab violet fill, `GuardrailBanner`, filters,
  and error/empty states render correctly; keyboard focus ring confirmed via
  real Tab navigation. Not click-through-verified: real queue/table rows or
  a full consequential-action flow — `useMyWork`, `useAgentWorkItems`, and
  `useApprovals` have no synthetic-mode fallback and the real backend isn't
  reachable in this environment; `StatusBadge`/`RiskBadge`/`ConfidenceBadge`
  render via the already-verified `StatusPill` and are typecheck-clean
  across every caller, but weren't individually eyeballed with real data.

  Screen 2 (`/app/work-items`) is now marked **Done**. Resuming at screen 3
  (`/app/claims`).

- 2026-09-08: screen 3 (`/app/claims`) — commit `083a07e`, preceded by a
  regression fix in commit `de2396d`. `ClaimsQueue.tsx` migrated onto
  `components/ui/Table` and tokens across all three responsive views (mobile
  card list, tablet condensed table, desktop full table); `+ New Claim
  (FNOL)` off `gradient-primary` onto `Button`'s flat-violet primary
  treatment; empty-after-filter is now visually distinct from empty-no-data
  (previously conflated — a filtered-to-zero result silently showed a bare
  table header).

  **`useClaims` has a `NEXT_PUBLIC_VISUAL_PARITY_MODE` synthetic fallback**
  (unlike screen 2's `useMyWork`/`useAgentWorkItems`/`useApprovals`), so this
  was the first screen verifiable end-to-end with real rendered data rather
  than typecheck + error-state-only. That verification caught a real
  regression from the `Primitives.tsx` foundation fix (`5b7c2e4`): several
  real callers pass a pre-formatted, spaced status string ("Missing
  Documents", "In Review", "Ready for Export", "Pending Approval") rather
  than the raw PascalCase enum name, and the rewrite had kept only the raw
  form, silently reintroducing the exact vocabulary bug it was meant to fix.
  Fixed in `de2396d` — every status in the synthetic fixture now confirmed
  via `getComputedStyle` against exact contract label + token RGB values.

  **Lesson for the remaining 36 screens:** when a screen's data hook has a
  parity-mode fallback, use it — it is meaningfully stronger verification
  than typecheck-clean + an error-state screenshot, and it just caught a
  real bug that both of those missed. Check each hook for this fallback
  before assuming the backend-unreachable environment limits verification to
  error states only.

  Screen 3 (`/app/claims`) is now marked **Done**. Resuming at screen 4
  (`/app/claims/[id]`).

- 2026-09-08: screen 4 (`/app/claims/[id]`) — commit `c272afc`, preceded by
  `GovernedActionDialog` migration (`47f1862`) and a real focus-return fix
  (`d7c8952`). By far the largest screen on the ledger: `ClaimReview.tsx`
  itself is 844 lines and composes 8 more files (~1,880 lines total) —
  `CustomerUpdatePanel`, `ClaimReviewRequestsPanel`,
  `CustomerUpdateGovernedDialog`, `PolicyQuestionPanel`, `CompletenessCard`,
  `MotorLifecycle`, `DocumentUploadWidget`, `MarkdownText`. This is the
  primary claim-review workspace — AI summary, evidence, completeness,
  fraud signals, and every consequential action an officer takes on a claim.

  `GovernedActionDialog` (the modal used for every consequential/audited
  action: export claim pack, confirm claim, and 7 more call sites across
  tiers 1/2/3/5) is used twice on this screen alone, so it was migrated
  first as its own foundation-level fix per §8.14 (Modal) — panel width
  460px/92vw max (was 560px), `--shadow-dls-modal` (the foundation slice's
  `--shadow-drawer` variable, confirmed to hold the exact value the design
  doc's §4 token table lists for `--shadow-modal`), ghost Cancel + primary/
  destructive confirm via `Button`. Live-verifying it against this screen's
  real "Export claim pack" trigger caught a genuine, pre-existing
  accessibility bug: the focus-trap effect's dependency array included
  `onClose` (a fresh closure every parent render), so any re-render of the
  800-line host screen while the dialog was open re-captured "previousFocus"
  from whatever was already focused inside the dialog, not the real trigger
  — focus was landing on `<body>` after Escape instead of returning to the
  trigger button. Fixed by capturing previousFocus in a ref set only on the
  true open/close transition. Confirmed fixed live via
  `document.activeElement` — this is exactly the kind of thing "keyboard
  walk: Escape+focus-return" in the per-screen verification checklist exists
  to catch, and it would have shipped broken across all 9 call sites without
  the live check.

  `useClaimReview` has a parity-mode synthetic fallback, so this screen was
  verified end-to-end with real data, not just typecheck + error states —
  confirmed the same claim (CLM-1048) shows "Ready for review" consistently
  between this screen and the `/app/claims` list (both now correctly
  translated via the `Primitives.tsx` vocabulary fix from screen 3's run).

  Verified: `pnpm lint`, `pnpm exec tsc --noEmit` (clean across the full
  9-file dependency tree plus all `GovernedActionDialog` callers), `check-
  ui-release-gates.mjs`, `pnpm test` (all suites), `pnpm build` (compiles;
  same pre-existing EPERM step) all pass. Browser-verified live at 1280px
  and 768px on `/app/claims/CLM-1048`: every content card, the completeness
  checklist, and the export-evidence-pack dialog all render correctly with
  real synthetic data.

  Screen 4 (`/app/claims/[id]`) is now marked **Done**. Resuming at screen 5
  (`/app/claims/[id]/extraction`).

- 2026-09-08: screen 5 (`/app/claims/[id]/extraction`) — commit `cd31e91`.
  `DocumentExtraction.tsx` migrated onto tokens: the compact review/source
  tab switcher, the document selector (new `Select` primitive), field and
  validation-warning cards, and the review-reason/action-button card.
  Approve extraction moved off `gradient-primary`.

  Confirmed, not fixed — a known limitation of `ConfidenceBadge`
  (`Primitives.tsx`) that this screen makes concrete: design language §8.22
  wants literal threshold wording ("0.71 · Below threshold"), but
  `DocumentItem`/field types (`lib/documentContracts.ts`) carry only a bare
  `confidenceScore`, no backend threshold to compare against — confirmed by
  reading that file directly. `ConfidenceBadge` renders relative High/
  Medium/Low tiers instead, which is honest about what it is without
  inventing a business threshold. A real fix needs a backend field; out of
  scope for a presentation-only migration.

  `useDocumentExtraction` has no parity-mode fallback, so this screen was
  browser-verified only via its real (unreachable-backend) error state,
  reached through a genuine extraction-review link generated from
  `/app/claims/CLM-1048`. The field-review and source-preview panels were
  not visually verified with real data.

  Verified: `pnpm lint`, `pnpm exec tsc --noEmit`, `check-ui-release-
  gates.mjs`, `pnpm test` (all suites), `pnpm build` (compiles; same pre-
  existing EPERM step) all pass. Browser-verified at 1280px and 768px.

  Screen 5 (`/app/claims/[id]/extraction`) is now marked **Done**. Resuming
  at screen 6 (`/app/documents`).

- 2026-09-09: foundation gap closed — side drawer primitive (design language
  §8.16). The previous run flagged this as "genuinely missing". It was in fact
  present as `components/SideDrawer.tsx` but unmigrated and, on inspection,
  **dead code with zero importers** — which is why it was not found. Built
  `components/ui/SideDrawer.tsx` to §8.16 (440px/92vw, hairline left border,
  `--shadow-drawer`, background made inert while open, focus trapped, Escape
  closes, focus returns to trigger) and deleted the dead file rather than
  keeping two drawers. The old one violated the language in five ways:
  `backdrop-blur-sm` (glassmorphism, banned), `rounded-l-2xl` (radius above
  16px, banned), 540px width, hardcoded "IIP Operations" eyebrow in every
  drawer, and legacy tokens including the `text-muted-soft` family that step 2
  retired for failing AA. Added `stories/SideDrawer.stories.tsx`.
  Verified: `pnpm exec tsc --noEmit`, `pnpm lint`, all five hygiene gate
  scripts, `pnpm test:trust-truth` (3 passed), `pnpm test:authority-permissions`
  (4 passed), and `pnpm build` with NEXT_PUBLIC_RUNTIME_PROFILE=Development
  NEXT_PUBLIC_AUTH_MODE=DevelopmentAuth (all routes generated).
  Still open, deliberately not fixed here: §8.20 requires a citation chip to
  open its source **in this drawer**. `CommandCenterAnswer.tsx` still renders
  citations as non-interactive chips because there is no document-preview
  capability to open into — that needs a source-region preview endpoint and
  should be scoped as its own task, not improvised inside a screen migration.

- 2026-09-09: foundation-level fix — commit `2f83869`. Found while starting
  screen 6: `SectionHeader` (`components/iip/Primitives.tsx`) — the header
  **35 of 39 screens actually import**, including three already-Done screens
  (`ClaimReview`, `ClaimsQueue`, `DocumentExtraction`) — was shipping a
  26px/28px h1 with no breadcrumb support, not the 23px/-0.41px h1 with
  optional §3.1 breadcrumb that design language §3.2 specifies. A
  spec-correct component already existed, `components/PageHeader.tsx`, built
  in the original foundation slice — but it had **zero real consumers**, only
  its own story file. Every screen kept using the differently-named
  `SectionHeader` instead, so the correct primitive never reached a screen.
  Same shape of gap as the `SideDrawer` finding one entry above: a correct
  primitive exists unmigrated, a same-purpose duplicate is what code actually
  calls. This one is worse in degree — 35 callers, not zero — which is why it
  is being fixed now rather than deferred: per the worklist's own stop
  condition, "the same problem on three or more screens" is a foundation gap,
  not a per-screen patch.

  Fixed `SectionHeader` in place to §3.1–3.2 exactly (lifted the breadcrumb
  JSX from `PageHeader.tsx`, which was otherwise correct) rather than
  touching 35 call sites. The `eyebrowClassName` escape hatch three screens
  use for the gate-17 AA contrast fix is preserved byte-for-byte. Removed
  `PageHeader.tsx` and its story as the dead duplicate; added
  `stories/SectionHeader.stories.tsx` in its place.

  Verified: `pnpm exec tsc --noEmit` clean, `pnpm lint` clean,
  `check-ui-release-gates.mjs` passed.

  **Every already-Done screen's header now renders 3px smaller with tighter
  tracking than when it was verified.** This is intentional — those screens
  become spec-correct, not regressed — but it means their Done status no
  longer reflects a browser-verified h1 at the size actually shipping.
  Flagging rather than silently letting the ledger claim a verification that
  didn't happen: a visual spot-check of `/app/claims`, `/app/claims/[id]`,
  and `/app/claims/[id]/extraction` is included in the screen-10 checkpoint
  below, not a full re-verification of each.

- 2026-09-09: foundation-level fix — commit `8c76cf7`. Also found while
  starting screen 6, in the same shared file: checking the document-library
  fixture's real status values against `DocumentProcessingStatus` in
  `iip_workflow_legibility_contract.md` §6/§10.6 turned up two vocabulary
  defects in `StatusBadge`'s lookup table. `"Reviewed"` had no entry and fell
  through to the default neutral tone instead of the contract's success tone.
  `"Review Required"` was rendering its own near-verbatim input text instead
  of the contract's canonical "Needs review" label, with the same wrong
  neutral tone instead of warning — the raw-label-leak pattern this table
  exists to prevent, just spaced English rather than PascalCase this time.
  Both fixed with the exact contract label + tone. Two further fixture
  values, `"Extraction Pending"` and `"Extracted"`, have no single confirmed
  `DocumentProcessingStatus` mapping verifiable against a real DTO — given
  tone-only corrections (info) rather than a guessed label rewrite.

  Verified: `pnpm exec tsc --noEmit` clean, `pnpm lint` clean. No test or
  story asserts the old wording.

- 2026-09-09: screen 6 (`/app/documents`) — commit `949205d`, preceded by the
  two `Primitives.tsx` foundation fixes above (both found while starting this
  screen). `Documents.tsx` migrated onto tokens and `components/ui/`
  primitives: the claim-finder search input onto `Input`/`leadingIcon`, claim
  result rows and the "selected claim" chip onto tokens, the main document
  table onto `Table`/`TableHead`/`TableBody`/`TableRow`/`TableHeaderCell`/
  `TableCell`. Upload document moved off `gradient-primary` onto `Button`'s
  primary variant.

  Split the previously conflated loading/error/empty-no-data states — error
  was folded into the empty-state message — into their own
  `LoadingSkeleton`/`ErrorStateSurface`/`EmptyStateSurface` renders per §8.24.
  empty-after-filter is inapplicable: the document list itself has no filter
  or search (only the upload panel's separate claim finder, a distinct
  control) — same accepted, documented partial as screen 1's precedent, not
  an invented feature to force the checklist item green.

  Found and fixed a real §8.24/AGENTS.md violation while migrating the error
  path: `lib/lovable-parity/data/documents.ts`'s `useDocuments` was throwing
  `` `Documents failed with HTTP ${response.status}.` `` and surfacing that
  string to the UI verbatim — a raw status code reaching the user. Replaced
  with a generic, safe message; the hook's return shape is unchanged. Also:
  zero-confidence documents now render plain muted "—" text instead of a
  `Badge`, which implied a status (present but low) that doesn't exist here.

  Verified: `pnpm exec tsc --noEmit` clean, `pnpm lint` clean,
  `check-ui-release-gates.mjs` passed. `useDocuments` has a visual-parity
  synthetic fallback. Per this run's verification philosophy, the full
  `pnpm test`/`pnpm build`/browser walkthrough is deferred to the checkpoint
  after screen 10, not run per screen.

  Screen 6 (`/app/documents`) is now marked **Done**. Resuming at screen 7
  (`/app/channels`).

- 2026-09-09: foundation-level fix — commit `968cf5a`. Found while starting
  screen 7: `lib/lovable-parity/data/channels.ts` had the exact same
  raw-HTTP-status-in-fallback defect fixed in `documents.ts` one entry above,
  three times over. A repo-wide grep for the same template turned up **67
  more occurrences across 29 files** — essentially the entire
  `lovable-parity` data layer, not a one-off. Fixing this per-screen, as it
  was found, would have meant rediscovering it on most of the remaining 33
  screens; fixed once, everywhere, instead.

  Read `uiLoadState.ts`'s `UiServiceError` and every file's local
  `safeError`/`readSafeApiError`/`readError` helper before touching anything:
  all of them already correctly prefer a backend-supplied `payload.message`/
  `payload.errors` value over the fallback, and `UiServiceError.fromResponse`
  already deliberately withholds raw 5xx body detail — the abstractions are
  sound. The defect was narrower and purely textual: the *caller-supplied
  fallback* strings baked the status code in, and that fallback is exactly
  what renders when the backend gives nothing better. Fix applied uniformly
  by regex across all 29 files: stripped `" with HTTP ${...}."` / `" (HTTP
  ${...})."` from every fallback, leaving every backend-message-preferring
  branch, return shape, error kind, and retry/correlation/permission-status
  branch untouched. Diffs spot-checked (`claimReview.ts`, 10 of the 67
  substitutions) before verifying.

  Verified: `pnpm exec tsc --noEmit` clean, `pnpm lint` clean across all 29
  files. No test or story asserted the old wording. Full `pnpm test`/
  `pnpm build`/browser verification deferred to the screen-10 checkpoint,
  consistent with this run's verification philosophy — but given the breadth
  of this change (29 files, not one screen), the checkpoint should include at
  minimum one error-state walkthrough on a screen using each of the three
  helper shapes (`UiServiceError.fromResponse`, a local `safeError`-style
  helper, and a bare thrown/returned template) rather than only the 5
  screens migrated since the last checkpoint.

- 2026-09-09: foundation-level finding, not yet fixed — flagging per the
  worklist's "same problem on three or more screens" stop condition rather
  than folding a fix into this screen. While building screen 7's channel
  filter tabs, found that `AgentWorkItems.tsx`, `ClaimsQueue.tsx`,
  `DocumentExtraction.tsx`, `ReviewWorkspace.tsx`, and `Workflows.tsx` (5
  occurrences, 4 already-Done screens) render an active filter/segmented tab
  as a **solid violet fill** (`bg-dls-primary text-dls-text-inverse`).
  Design language §8.11 specifies a **light tint**: `bg-dls-primary-soft-alt`
  background, `border-dls-primary-border`, `text-dls-primary-accent` — solid
  violet fill is §8.1's primary-button treatment, not a tab's. The correct
  tint pattern already exists, used correctly elsewhere in the same files for
  selected list rows and inline info panels — this is a same-shape-different-
  spot mistake, not confusion about the tint tokens' existence.

  Built `components/ui/FilterTab.tsx` to §8.11 exactly and used it for this
  screen's own channel-filter tabs (see the screen-7 entry below) rather than
  copying the existing wrong pattern into a sixth location. Deliberately
  **not** touching the 5 existing occurrences here: that means re-opening 4
  already-Done, already-verified screens outside the one currently in
  flight, which the ledger's own resume rule ("never have two screens in
  flight") argues against doing casually mid-screen. Recommend a dedicated
  follow-up commit — migrate all 5 call sites onto `FilterTab`, re-verify
  only those 4 screens (not the whole worklist) — before or shortly after
  the screen-10 checkpoint, so the defect doesn't keep compounding if a
  later screen's author copies the visible (wrong) pattern instead of
  finding this note.

- 2026-09-09: screen 7 (`/app/channels`) — commit `04704f5`, preceded by the
  two fixes above (both found while starting this screen). `Channels.tsx`
  migrated onto tokens and `components/ui/` primitives, closing out tier 1.

  Found and fixed a genuine "does nothing" defect, not just a re-skin: the
  conversation-list search box was decoration — a `Search` icon and
  placeholder-like static text with no `<input>`, no `onChange`, wired to
  nothing. Craft guardrails §2 item 25 ("buttons that do nothing") applies
  equally to a fake search box. Rebuilt as a real `Input` filtering the
  already-fetched `conversations` list client-side (no new query — same
  presentation-only class of addition as `ClaimsQueue`'s pre-existing
  client-side search). This made a genuine empty-after-filter state possible
  for the first time on this screen; added it via `EmptyFilteredStateSurface`
  alongside the now-properly-split loading/error/empty-no-data states (error
  was previously folded into the empty block via a ternary on the message
  text — the same conflation pattern fixed on screens 3 and 6).

  Channel filter pills moved onto the new `FilterTab` primitive. Message
  bubbles moved off `gradient-primary` onto a flat violet-fill/neutral-surface
  treatment matching §8.17's visual grammar, hand-rolled rather than
  importing `ChatPrimitives` — this is a customer-conversation thread, a
  different domain from the AI Assistant chat surface, sharing the look
  without sharing the component. The inline WhatsApp warning callout moved
  off an ad-hoc `bg-amber-50`/`text-amber-900` pairing onto the
  `dls-warning-tint`/`dls-warning-text` tokens.

  Verified: `pnpm exec tsc --noEmit` clean, `pnpm lint` clean,
  `check-ui-release-gates.mjs` passed. `useChannelConversations` and
  `useWhatsAppThread` have no visual-parity synthetic fallback for live list/
  transcript data (`parityMode` only covers the top-level empty-state demo
  transcript), so real-data browser verification of the list and transcript
  is deferred to the screen-10 checkpoint, along with `pnpm test`/
  `pnpm build`.

  Screen 7 (`/app/channels`) is now marked **Done — tier 1 complete**.
  Resuming at screen 8 (`/app/conversations`), tier 2.
