# UI migration ledger — design language v2

Tracks screen-by-screen migration of `web/portal` onto the design language v2
foundation (`iip_design_language_v2.md`). One row per screen. A row is marked
**Done** only after it has been opened in a browser, verified against the
27-point checklist in `iip_ui_craft_guardrails.md` §8, and committed.

Resume rule: find the first row not marked Done and migrate that screen next.
Never have two screens in flight.

| # | Tier | Route | Component | Status | Date | Commit |
|---:|---|---|---|---|---|---|
| 1 | 1 | `/app` | `lovable-pages/AIConversations.tsx` | Blocked — see run log 2026-09-08 | 2026-09-08 | `d0a12d6` |
| 2 | 1 | `/app/work-items` | `lovable-pages/ReviewWorkspace.tsx` (+ UniversalMyWork, AgentWorkItems, Workflows) | Not started | | |
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
