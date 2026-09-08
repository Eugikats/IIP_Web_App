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
| 3 | 1 | `/app/claims` | `lovable-pages/ClaimsQueue.tsx` | Not started | | |
| 4 | 1 | `/app/claims/[id]` | `lovable-pages/ClaimReview.tsx` | Not started | | |
| 5 | 1 | `/app/claims/[id]/extraction` | `lovable-pages/DocumentExtraction.tsx` | Not started | | |
| 6 | 1 | `/app/documents` | `lovable-pages/Documents.tsx` | Not started | | |
| 7 | 1 | `/app/channels` | `lovable-pages/Channels.tsx` | Not started | | |
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
