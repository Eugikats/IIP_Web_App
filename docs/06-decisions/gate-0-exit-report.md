# Gate 0 — Exit Report (Secure baseline · UI/backend alignment · real API contract map)

| Field | Value |
|---|---|
| Gate | 0 — Secure baseline, UI/backend alignment, real API contract map (AGENTS.md §25) |
| Branch | `feature/lovable-design-parity` (PR #1 → `main`) |
| Date | 2026-07-06 |
| Status | **In progress** — Pillars A (secure baseline) and C (contract map) complete; Pillar B (alignment builds) partially complete |

This is the living sign-off document for Gate 0. It states, per pillar, what is real vs
honest-gated (with reasons), the evidence, and the conservative readiness position. Gate 0 is
**not** declared complete here — the owner declares it complete against this report.

---

## Pillar A — Secure baseline ✅ complete

| Item | Status | Evidence |
|---|---|---|
| Security response headers (CSP, nosniff, anti-frame, referrer) | ✅ | `Program.cs` middleware |
| CORS locked to portal origin | ✅ | `Program.cs` `AddCors` (localhost:3000/127.0.0.1:3000) |
| Demo identity headers rejected outside Development | ✅ | `RuntimeHeaderGuard` |
| Unauthenticated `/api/v1` rejected outside Development | ✅ | `RuntimeHeaderGuard` |
| Zero-simulation startup validation (Demo/Staging/Prod/Reviewer) | ✅ | `AzureProviderStartupValidator` + tests |
| **A1** Debug/test endpoints gated to Development-only | ✅ | `debug-teams-notification`, `email/test-ingestion` mapped only when Development/LocalTest (commit `fe68b1c`) |
| **A4** Authorization coverage audit | ✅ | 85/90 endpoints permission-filtered; 5 exempt are legitimate (3 health probes, self-scoped `/me`, payload-validated Graph webhook). Claims endpoints enforce at the service layer (`PermissionDeniedException`). Full table in `docs/api/IIP_API_Contracts_Master.md` §7. |
| **A2** Dependency vulnerability scan | ✅ | `.NET`: 0 vulnerable packages (all projects). `pnpm`: 1 **low**, dev-only (`elliptic` via `@storybook/nextjs`, not in the runtime bundle). Triaged acceptable; revisit if Storybook is removed. |
| **A3** No committed secrets | ✅ | Scan of tracked files: no value-bearing secrets. The one connection-string hit is a Bicep `listKeys()` deploy-time expression (correct pattern). `.env.local` gitignored; no `.env`/secrets/appsettings.Production committed. |

**Correction on record:** an earlier pass flagged workflow/approval endpoints as unauthorized.
That was a false alarm caused by a route-extraction regex that dropped two-argument
`PermissionEndpointFilter(...)` calls. Verified: those endpoints are permission-gated.

## Pillar C — Real API contract map ✅ complete

| Item | Status | Evidence |
|---|---|---|
| **C1** Route-versioning ADR | ✅ | `infra/ADR-0004-api-route-versioning.md` — `/api/v1` canonical (commit `fe68b1c`) |
| **C2** Generated OpenAPI + dev-only Swagger | ✅ | `docs/api/openapi.json` (89 paths); Swagger UI + JSON exposed Development/LocalTest only (commit `aa4161d`) |
| **C3/C4** Reconciled contract master + endpoint→UI→permission map | ✅ | `docs/api/IIP_API_Contracts_Master.md` — real `/api/v1` routes, real `Permission` enum, bare-DTO reality, verified 90-endpoint table, screen→API map, 5 reconciliation deltas (commit `e3ef89e`) |

**Envelope decision:** the API returns bare DTOs today; a uniform `ApiResponse<T>` envelope is
deferred to a future ADR (not a silent Gate 0 change). See contract master §3.2.

## Pillar B — UI/backend alignment 🟡 partial

Decision (owner): build the now-buildable screens; honest-gate only genuinely Gate-3
provider-dependent surfaces.

| Item | Status | Notes |
|---|---|---|
| Prior parity work (Fraud, Underwriting, Agents, Billing, Audit, Executive, Claims list, Customers, Documents, Channels read, Knowledge, Workflows) | ✅ | Reconciled to real `/api/v1` in earlier commits |
| Two review modes (Demo/Production) + environment banner | ✅ | Committed earlier this branch |
| **B2** Command Center → real `/command-center/query` | ✅ done | AI command bar POSTs to the real planner; `CommandCenterAnswer` renders all 12 governed block types + safe unknown fallback (commit after `b75f862`) |
| **B3** Sales-Leads manual create + list + persist | ⏳ pending | Manual origination now; channel ingestion + fabric import honest-gated (Gate 3) |
| **B4** Multi-workspace data model + `/me` available-workspaces | ⏳ pending | Data model now; switcher UI deferred |
| **B1** ClaimReview full governed workspace | ⏳ pending | Wire existing detail/extraction/timeline/approval endpoints |
| **B5** Remove customer login portal from deployed surface | ⏳ pending | Out of scope per PRD. Entangled with `Client` role + `/app/portal` + visual specs — needs a focused, verified role-model change (not rushed). |

### Genuinely honest-gated for Gate 0 (correct end-state until Gate 3)
- Policy Intelligence grounded Q&A (RAG) — needs Azure AI Search + OpenAI (Gate 3).
- Channels write-actions — no conversation ingestion source until Gate 3.
- Sales-Leads channel ingestion + integration-fabric import — Gate 3 / integration phase.
- Email/messaging send surfaces — Microsoft Graph (Gate 3).

## Evidence — automated checks (this session)

- `dotnet build InsuranceIntelligence.sln` — **0 errors**.
- `dotnet test InsuranceIntelligence.sln` — **237 API + 95 Domain pass**.
- `pnpm lint` — clean (one pre-existing `<img>` hint in `Sidebar.tsx`).
- `pnpm build` — succeeds.
- `dotnet list package --vulnerable` — 0 vulnerable. `pnpm audit` — 1 low (dev-only).

## Apps in scope

- **IIP Operations** (`/app/*`) — primary, in scope.
- **Admin surfaces** (agents, billing, users, tenant config) — in scope.
- **Customer login portal** — **out of scope** (PRD: customers use channels; staff app is internal).

## Release readiness (conservative)

```text
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

Gate 0 does not touch identity/staging/packaging (Gates 1–6). Sign-off requires Pillar B
items B1–B5 complete (or explicitly accepted as honest-gated by the owner).

## Remaining to close Gate 0

1. ~~B2 Command Center query wiring.~~ ✅ done.
2. B3 Sales-Leads manual create + persistence.
3. B4 Multi-workspace data model + `/me` available-workspaces.
4. B1 ClaimReview governed workspace.
5. B5 Remove customer login portal from deployed surface (role-model change).
6. Live screenshots of the newly-built screens (owner review).
