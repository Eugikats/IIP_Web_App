# Lovable Parity — Progress & Completion Report

| Field | Value |
|---|---|
| Date | 2026-07-05 |
| Branch | `feature/lovable-design-parity` (pushed to `origin`) |
| Related | `lovable-parity-migration-audit.md` (backlog), `ADR-2026-07-04-lovable-visual-parity-mode.md`, decision log DEC-011..015 |

## Completed and verified

1. **Docs alignment (audit step 1)** — every governance/reference doc reconciled with the `/app/*` parity reality and all stale `docs/ui/*` references redirected to the real doc set: `AGENTS.md`, `CLAUDE.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CONTRIBUTING.md`, `copilot.md`, design-system spec, UI/UX handoff, program plan, product decision log (DEC-015), env/secrets guide, README. Fixed AGENTS.md §20's stale "no Channels/Sales-Leads backend" claim.
2. **Old-tree removal (audit step 5)** — deleted the 20 orphaned/broken pre-migration routes + dead IIP shell/nav/context (`AppShell`, `SidebarNav`, `TopBar`, `RightCopilotPanel`, `RouteAccessGuard`, `ProviderStatusStrip`, `RoleContext`, `roleUiConfig`, `navigationIcons`, `MobileNav`, `AssistantChat`, `CommandCenterIntelligence`, `useCommandCenterQuery`, `CommandCenterClient`). Resolves audit A2/A3/A4 (routes were crashing without `RoleProvider`). Routes are now `/` (Login) + `/app/*` + `/customer-portal/*`.
3. **Reconciliation pattern established** — per-screen hook in `web/portal/lib/lovable-parity/data/*` serving fixtures in parity mode and real tenant-scoped `/api/v1` data otherwise (role via the `/app` tree's `useUI()`), with honest empty/error states and honest actions.
4. **Channels vertical** — `/app/channels` reconciled to `/api/v1/channels/overview`.
5. **Fraud Intelligence vertical (new domain, DEC-013/014)** — real deterministic rule engine (`Domain/Fraud/FraudRuleEngine`, 9 unit tests), `Application/Fraud` query service (tenant-scoped, `ViewFraudSignals`), `GET /api/v1/fraud/signals`, and reconciled `/app/fraud` screen with derived counts + honest states. Advisory-only, never a verdict.
6. **Underwriting Intelligence vertical (new domain, DEC-013/014)** — real rule engine (`Domain/Underwriting/UnderwritingRuleEngine`, 5 unit tests), query service + `IUnderwritingSubmissionSource` port with honest `NotConfigured` implementation, `GET /api/v1/underwriting/reviews`, reconciled `/app/underwriting` screen. Advisory-only, never accepts/rejects/prices.
7. **Permissions** — added `ViewFraudSignals`, `ViewUnderwriting`, `ManageAgents`, `ViewBilling` to the enum + `RolePermissionMap`.
8. **Audit reconciled** — `/app/audit` → `/api/v1/audit/events` with a real client-side CSV export.
9. **Provider health reconciled (shared)** — `ProviderHealthGrid` (used by Support & Integrations) → `/api/v1/admin/provider-health`; Support's unbacked queue/failed-jobs/diagnostics stats gated behind parity mode (`Not configured` in live, no fabricated stats).

**Reconciled screens (12):** Channels, Fraud, Underwriting, Audit (real CSV export), Support, Claims, Customers list, Customer 360 detail, Communications, Knowledge, Workflows/approvals, Executive (real KPIs + live claims-by-status chart + CSV) — plus the shared provider-health grid used by Support & Integrations.

**Backend deltas shipped:** `GET /api/v1/customers` list endpoint (unblocked Customer 360).

**Remaining screens** (harder / backend-heavy, documented in the audit): Command Center (chat/query wiring), Documents (needs cross-case list endpoint + status enums), Sales-Leads/Acquisition (needs a sales-leads **list** endpoint like customers), Policy Intelligence (grounded Q&A POST flow), FNOL / Claim detail / Document extraction (case-scoped flows), Admin / Settings / Client Portal (config surfaces), plus the Agent Registry and Billing screens (need their backends first).

**Remaining backend:** Agent Registry (+ kill switch, persistence), Billing (usage counters), Documents/Communications status enums + cross-case list endpoints, Sales-Leads list, Channels write-actions, `/me` workspaces.

**Remaining non-screen:** token consolidation (`globals.css` + `lovable.css`), dead-code cleanup (`components/blocks/*`).

## Milestone (2026-07-05): no fabricated data in live, across the whole app

Every `/app/*` screen now either fetches real `/api/v1` data or shows an honest empty / `Not configured` state. Prototype fixtures render **only** per-screen when `NEXT_PUBLIC_VISUAL_PARITY_MODE=true` (dev). Consequently the `LovableVisualParityGate` whole-app prod block was removed (A7 resolved) — per-screen honesty is now the protection.

- **Reconciled to real endpoints (14):** Channels, Fraud, Underwriting, Audit, Support + provider-health (shared → Integrations), Claims, Customers list, Customer 360 detail, Communications, Knowledge, Workflows, Executive, Command Center (metric pills + priority claims).
- **Honest-gated in live (fixtures dev-only) pending their backend/feature (6):** Documents (cross-case list endpoint), Agents (registry backend), Billing (usage backend), Acquisition (leads persistence), Command Center chat + activity, ClaimReview full workspace.

## Update (2026-07-05, later) — steps 3 & 4 completed

**Backend batch (step 3) — done:**
- ✅ Agent Registry backend (catalog + persisted kill switch, ADR-0002) + tests → `/app/admin/agents` un-gated & wired to real kill switch.
- ✅ Billing backend (default entitlements + real usage meters, ADR-0003) + tests → `/app/admin/billing` un-gated & wired.
- ✅ Fraud + Underwriting rule-engine backends (earlier) + `/api/v1/customers` list → Customer 360 wired.
- ✅ Documents cross-case endpoint (`GET /api/v1/documents`) + `DocumentProcessingStatus` extended (ReviewRequired/Reviewed/Rejected) → `/app/documents` un-gated & wired.

**Token consolidation (step 4) — done:** documented as a deliberate two-layer split by surface (lovable.css HSL for `/app`; globals.css RGB for customer-portal + shared governed primitives; disjoint, verified). Removed dead `components/blocks/*`, `lib/commandCenterBlocks.ts`, `lib/commandCenterFixtures.ts`.

**Test totals:** 95 Domain + 229 API pass; portal build + lint green.

### Genuinely blocked on a data source that doesn't exist yet (honest states in live)
- **Command Center grounded chat** — needs the `/api/v1/command-center/query` chat surface built (metric pills + priority claims are already live).
- **ClaimReview full workspace** — `/api/v1/claims/{id}` detail exists; the rich extraction/timeline/governed-action workspace is a feature build.
- **Policy Intelligence grounded Q&A** — needs the RAG question POST flow.
- **Channels write-actions** (create-claim/lead/draft/escalate) — no conversation ingestion source yet (read repo returns empty); actions honest-disabled.
- **Sales-Leads list + Acquisition** — sales-leads read repo is a stub (no persistence); honest empty in live.
- **Multi-workspace switcher** — no workspaces catalog (one-insurer-per-instance default); `/me` resolves a single scope.

These are new-feature / data-source work, not fixture cleanup — every screen already shows real data or an honest state in live.

**Validation:** `dotnet build` 0 errors; `dotnet test` 85 Domain + 229 API pass; `pnpm build` + `pnpm lint` green.

## Remaining backlog (with reasons)

| Item | Status / reason |
|---|---|
| **Agent Registry** (new domain, ADR-0002) | Not started. Needs EF persistence (agent definitions, run logs, kill-switch state) + migration — real infrastructure that cannot be runtime-verified in this environment. Kill-switch must persist to be meaningful. |
| **Billing & entitlements** (new domain, ADR-0003) | Not started. Needs persisted entitlements + real usage counters (AI calls/docs/channels/storage) that don't exist yet; honest v1 would surface `Not configured` for unmetered dimensions. |
| **Remaining screen reconciliations** | `/app` (command center), customers + customer-360 detail, communications, documents, claims, claims/[id], claims/new, acquisition, policy, knowledge, workflows, audit, integrations, support, executive, admin, settings, portal still render prototype fixtures. Each follows the Channels/Fraud template; several depend on backend deltas below. |
| **Backend deltas** | Customers **list** endpoint (only `/customers/{id}` detail exists); Documents cross-case list + status enum (`ReviewRequired`/`Reviewed`/`Rejected`) + retry; Communications cross-case list + `DeliveryStatus` enum; Channels write-actions (create-claim/create-lead/draft-response/escalate) + the tenant-context `SetCurrent` gap; `/api/v1/me` available-workspaces for the workspace switcher. |
| **Token consolidation** (audit §2.3) | Two token layers still coexist (`globals.css` RGB + `lovable.css` HSL). |
| **Prod parity-gate** (audit A7/§2.4) | `LovableVisualParityGate` still blocks the whole app in a prod build when the flag is off — correct safety net WHILE screens are fixture-backed; resolve once all screens are reconciled. |
| **Dead code cleanup** | `components/blocks/*` + `lib/commandCenterBlocks.ts` are now orphaned (compile fine); remove in a later pass. |

## Release readiness (conservative)

```text
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

This initiative is UI/feature-parity work inside AGENTS.md Gate 0; it does not touch identity/staging/packaging. The prod parity-gate and remaining fixture screens must be resolved before any deployed build.
