# Lovable Parity Migration — Codebase & Docs Audit

| Field | Value |
|---|---|
| Date | 2026-07-05 |
| Author | Claude (this clone) |
| Trigger | Deep audit after the `LovableVisualParity` migration (`47ed7a8`) landed on `feature/lovable-design-parity` |
| Related | `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md`, `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` (Initiative LP), `iip_product_decision_log.md` DEC-011..014 |
| Purpose | Enumerate exactly what files and docs need to change to converge on the adopted `/app/*` parity approach |

## Executive summary

The migration introduced a **second, parallel frontend** (`app/app/*`, 26 routes rendering the Lovable prototype from `lovable-pages/*`) with its **own shell** (`components/iip/AppShell` + `Sidebar`), its **own token layer** (`app/lovable.css`), and its **own role/UI context** (`context/UIStateContext` + `lib/roles.ts`). The decision (ADR-2026-07-04 + user) is that **`/app/*` is canonical**. That makes most of the pre-migration frontend — 20 top-level routes, the IIP shell, `roleUiConfig`, and one of the two token systems — **redundant and, as of the root-layout change, broken**. The work ahead is: (1) reconcile each `/app/*` screen off fixtures onto real `/api/v1` data, (2) build the four net-new backend domains those screens need, (3) remove the orphaned old tree, (4) consolidate tokens, and (5) update the governance docs that still describe the old structure.

---

## 1. Architecture divergences (root causes)

| # | Divergence | Detail | Consequence |
|---|---|---|---|
| A1 | **Two route trees** | Old top-level routes (`app/channels`, `app/customers`, …, 20 of them) **and** new `app/app/*` (26 routes). | Duplicated screens; only one tree can be canonical (`/app/*`). |
| A2 | **Root layout dropped the IIP shell** | `app/layout.tsx` now wraps children in only `ThemeProvider` + `LovableVisualParityGate`. It no longer mounts `components/AppShell` (which provided `RoleProvider`, `SidebarNav`, `TopBar`). | **The 20 old routes are now broken**: no sidebar/topbar, and any client using `useRole()` (e.g. old `ChannelsClient`, `CustomersClient`) throws "useRole must be used inside RoleProvider." They are dead. |
| A3 | **Two shells** | `components/AppShell.tsx` (IIP, now unused by any layout) vs `components/iip/AppShell.tsx` (prototype, used by `app/app/layout.tsx`). | IIP shell + its children are dead code once old routes go. |
| A4 | **Two role/nav systems** | IIP `lib/roleUiConfig.ts` (points at old routes) + `components/RoleContext.tsx` vs prototype `lib/roles.ts` + `context/UIStateContext.tsx` + `components/iip/Sidebar.tsx` (points at `/app/*`). | Nav config duplicated; roleUiConfig is now orphaned. |
| A5 | **Two token systems** | `app/globals.css` (RGB `--color-*` scale) + `app/lovable.css` (HSL `--navy`/`--radius`/shadcn set); both imported in `layout.tsx`; `tailwind.config.ts` maps both (`token()` + `hslToken()`). | Redundant/overlapping tokens; must consolidate to one. |
| A6 | **Root `/` is now the prototype Login** | `app/page.tsx` → `@/lovable-pages/Login`; the app lives at `/app/*`. | Contradicts AGENTS.md ("Command Center is the landing route at `/`"). |
| A7 | **Whole app gated by parity flag in prod** | `LovableVisualParityGate` (in root layout) blocks **all** routes in `NODE_ENV=production` unless `NEXT_PUBLIC_VISUAL_PARITY_MODE=true`. | A real production build currently shows a "disabled" page for everything — a migration artifact that must be resolved before any deploy. |

---

## 2. Frontend files to update

### 2.1 Reconcile `/app/*` screens off fixtures → real `/api/v1` (the core work)

Pattern established on Channels: a per-screen hook in `web/portal/lib/lovable-parity/data/*.ts` that serves fixtures in parity mode and real tenant-scoped API data otherwise (role via `useUI()`), with honest empty/error states and honest actions. Apply to every prototype screen:

- **Done:** `lovable-pages/Channels.tsx` (+ `lib/lovable-parity/data/channels.ts`).
- **API-backed, reconcile now** (real endpoints already exist): `Customers.tsx`, `CustomerDetail.tsx`, `Communications.tsx`, `Documents.tsx`, `ClaimsQueue.tsx`, `ClaimReview.tsx`, `DocumentExtraction.tsx` (FNOL), `FNOL.tsx`, `Workflows.tsx` (approvals), `Audit.tsx`, `Knowledge.tsx`, `PolicyIntelligence.tsx`, `Integrations.tsx`, `Support.tsx`, `Executive.tsx` (reports), `CustomerAcquisition.tsx` (sales-leads), `AIConversations.tsx`/`CommandCenter.tsx` (command center), `ClientPortal.tsx`, `Admin.tsx`, `Settings.tsx`.
- **Blocked on new backend** (see §3): `FraudIntelligence.tsx`, `Underwriting.tsx`, `admin/Agents.tsx`, `admin/Billing.tsx`.
- All still contain prototype fakes to remove: hardcoded sample threads, `toast.success("… created")` fake-success actions, and `lib/lovable-parity/mock` imports (15 files still import it).

### 2.2 Remove the orphaned old tree (after nav fully points at `/app/*`)

- Delete the 20 old route folders under `web/portal/app/` (`channels`, `customers`, `claims`, `cases`, `approvals`, `compliance`, `customer-service`, `document-checker`, `integrations`, `knowledge-base`, `policy-intelligence`, `reports`, `sales-leads`, `support-health`, `tenant-onboarding`, `onboarding`, `workspaces`, `ai-assistant`, `admin-settings`, `client-portal`) and their `*Client.tsx`.
- Delete now-dead IIP shell/nav: `components/AppShell.tsx`, `SidebarNav.tsx`, `TopBar.tsx`, `RightCopilotPanel.tsx`, `MobileNav.tsx`, `RouteAccessGuard.tsx`, `ProviderStatusStrip.tsx`, `lib/roleUiConfig.ts`, `components/RoleContext.tsx` — **only** once nothing imports them (my reconciled hooks use `useUI()`, not `RoleContext`, so this is clean).
- Note: `customer-portal/*` is a separate customer-safe tree — confirm whether `/app/portal` (prototype ClientPortal) supersedes it before removing.

### 2.3 Consolidate tokens (A5)

- Fold `app/lovable.css` values into `app/globals.css` as the single source, or vice-versa; drop the duplicate. Reconcile the RGB (`token()`) vs HSL (`hslToken()`) split in `tailwind.config.ts` to one convention. Keep the Ambient Intelligence values (DEC-011).

### 2.4 Resolve the prod gate (A7)

- `components/iip/LovableVisualParityGate.tsx`: it should gate only the *fixtures/parity screens*, not block the entire production app. Decide the deploy story (parity mode is dev-only; production must render the reconciled real screens, not a "disabled" page).

---

## 3. Backend files to add (new domains for `/app/*` screens)

These `/app/*` screens have **no backend**; per DEC-013 they are full features, not mocks. Build each following the Phase-2 query-service pattern (`Application/<F>/` contracts+service → `Api/<F>/` endpoints → `Infrastructure/Persistence` repo → DI in `Program.cs` + `InfrastructureRegistration.cs` → tests), then reconcile the screen:

- **AI Agent Registry + kill switch** → `/app/admin/agents` (ADR-0002). Investigate reusing `Domain/Agents/AgentContracts.cs`.
- **Fraud Intelligence** (rule engine, advisory only) → `/app/fraud` (DEC-014).
- **Underwriting Intelligence** (rule engine, never decides) → `/app/underwriting` (DEC-014).
- **Billing & entitlements** (real usage meters) → `/app/admin/billing` (ADR-0003).

Also needed by existing screens being reconciled:
- **Documents**: extend `DocumentProcessingStatus` with `ReviewRequired`/`Reviewed`/`Rejected` + a cross-case list endpoint + retry-extraction.
- **Communications**: replace free-text `DeliveryStatus` with an enum (`PendingApproval`/`Approved`/`Sent`/`SendFailed`/`Rejected`) + a cross-case list endpoint.
- **Customer 360**: `GET /api/v1/customers/{id}` detail + AI-summary(cited)/next-best-action fields.
- **Channels write-actions**: create-claim/create-lead/draft-response/escalate + fix the tenant-context `SetCurrent` gap on that route group.
- **Workspace switcher**: `/api/v1/me` must return the user's available workspaces.

---

## 4. Docs to update

| Doc | What's stale / needed |
|---|---|
| `CLAUDE.md` | Still points to non-existent `docs/ui/*`. Update UI-authority section to the real doc set + the parity-mode ADR; note `/app/*` is the app and `/` is Login. |
| `AGENTS.md` | §19 references non-existent `docs/ui/*`; §20 says "no Channels/Sales-Leads backend" (now exists — stale); "Command Center is the landing route at `/`" contradicts A6. Reconcile all three. |
| `IIP_IMPLEMENTATION_GUARDRAILS.md` | §21.3 references non-existent `docs/ui/*`; §21 says Command Center at `/command-center`/`/` is primary — update for `/app/*`. |
| `CONTRIBUTING.md`, `copilot.md` | Reference `docs/ui/*`; update route/structure guidance. |
| `docs/03-design-architecture/iip_design_system_ux_behavior_specification.md` | My v1.2 update is visually correct but describes reconciling *real routes* + assumes one token system; add the `/app/*` parity-mode reality + token consolidation. |
| `docs/02-development-engineering/iip_ui_ux_handoff_document.md` | Screen specs 9.17–9.25 assume real-route rebuild; re-frame around the `/app/*` tree + `lovable-pages/` + `lib/lovable-parity/data` reconciliation pattern. |
| `docs/08-program-plan/IIP-FULL-PROGRAM-PLAN.md` | Initiative LP describes the real-route rework approach (now superseded). Rewrite LP.1–LP.7 as the `/app/*` reconciliation + old-tree removal + token consolidation + backend-domains plan, aligned to ADR-2026-07-04. |
| `docs/01-strategy-requirements-roadmap/iip_product_decision_log.md` | DEC-012 wording ("bring existing routes to parity") no longer matches the `/app/*` tree approach; add a DEC (or amend) recording the parity-mode-canonical decision + old-tree removal. |
| `docs/05-deployment-release-enablement/iip_environment_variables_secrets_guide.md` | Document `NEXT_PUBLIC_VISUAL_PARITY_MODE` (currently only in the ADR) and the prod-gate resolution (A7). |
| `web/portal/README.md` / root `README.md` | Update run instructions (parity mode env var) and the route map. |
| `docs/06-decisions/ADR-2026-07-04-lovable-visual-parity-mode.md` | Accurate; keep. Its "follow-up backend contract targets" list is the reconciliation backlog — cross-link it to this audit. |

---

## 5. Config / hygiene

- `web/portal/scripts/check-runtime-data-hygiene.mjs` — good addition (scans `app`/`components`/`lib` for banned mock strings, allowlisting `lib/lovable-parity`). Ensure it runs in CI and that reconciled screens don't trip it. Fixtures are correctly confined to `lib/lovable-parity`.
- `tailwind.config.ts` content globs now include `./lovable-pages/**` and `./context/**` — correct for the new tree.
- Confirm `.env`/example documents the parity flag.

---

## 6. Recommended sequence

1. **Docs alignment first** (§4) — so both clones build against the `/app/*` reality (doc-first discipline). Especially the program plan + AGENTS.md route/landing facts.
2. **Reconcile API-backed `/app/*` screens** (§2.1) — batch by shared data source; Channels is the template.
3. **Backend domains** (§3) — Agents, Fraud, Underwriting, Billing + the status-enum/endpoint deltas; then reconcile their screens.
4. **Token consolidation** (§2.3) and **prod-gate resolution** (§2.4).
5. **Remove the orphaned old tree** (§2.2) — last, once nav + reconciliation fully cover `/app/*`.
6. **Validation + completion report.**
