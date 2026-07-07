# ADR-0004 — API Route Versioning Strategy (`/api/v1` is canonical)

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Product, Architecture, Backend, Frontend
- **Gate:** Gate 0 (secure baseline / UI-backend alignment / real API contract map)
- **Related:** AGENTS.md §26 ("API route versioning, if /api/v1 is introduced later"); `docs/api/IIP_API_Contracts_Master.md`

## Context

AGENTS.md §26 requires an ADR whenever API route versioning is introduced. The IIP backend already exposes **every** endpoint under `/api/v1/…` (~90 routes across claims, documents, approvals, command-center, admin, integrations, communications, channels, customers, sales-leads, audit, fraud, underwriting, agents, billing). No un-versioned `/api/…` routes exist.

The draft `IIP_API_Contracts_Master.md` was written with `/api/…` as the canonical style and warned against mixing `/api/…` with `/api/v1/…`. That guidance is **backwards relative to the implementation**: the real system standardized on `/api/v1/` before the contract doc was drafted. This ADR ratifies reality rather than churning ~90 endpoints (and the entire frontend) for a cosmetic prefix change with no functional benefit.

## Decision

1. **`/api/v1/…` is the canonical route style for all IIP HTTP endpoints.** Health probes (`/health`, `/health/live`, `/health/ready`) are intentionally un-versioned infrastructure endpoints and are exempt.
2. **No un-versioned `/api/…` business routes.** New endpoints are added under `/api/v1/…`.
3. **The contract map (`docs/api/IIP_API_Contracts_Master.md`) and generated `openapi.json` use the real `/api/v1` routes.** The draft's `/api/…` examples are corrected to `/api/v1/…`.
4. **All endpoints move together as one version.** IIP is a single-app platform (IIP Operations); there is no independent consumer of a subset of routes that would justify per-endpoint versioning.

## Introducing `/api/v2` in the future

A future breaking change introduces `/api/v2` under a new ADR that must specify:

- Why the break is needed and which endpoints move.
- Whether `v1` and `v2` run concurrently and for how long (deprecation window).
- How the generated TypeScript client and frontend hooks migrate.
- How `openapi.json` exports both versions (or separate documents).
- Impact on any external/integration clients.

Until such an ADR exists, `v2` must not appear in the codebase.

## Consequences

- **Positive:** Zero code churn; the running app, tests, and frontend continue to work. The contract map becomes truthful. Future versioning has a defined path.
- **Negative:** The draft contract doc's `/api/…` convention is overridden — anyone following that draft must use `/api/v1/…` instead. Mitigated by reconciling the doc in the same Gate 0 batch.
- **Neutral:** Health endpoints remain un-versioned by design.
