# Live Performance Test Approval

**Environment:** Internal dev API
**Status:** Approved bounded tests executed; latency gate failed
**Date:** 2026-07-13

The repository owner approved bounded remote readiness and authenticated claims-list performance tests against the internal dev API. This approval does not cover staging, production, document uploads, provider calls, mutation routes, claim-detail load, or stress/soak testing.

## Approved envelope

- Target: `https://app-api-dev-h5girbnzrayqe.azurewebsites.net`
- Routes: `GET /health/ready` and `GET /api/v1/claims`
- Load: 50 requests per run, concurrency 5
- Runs: two readiness-only runs followed by one readiness plus authenticated claims-list run
- Data: server-resolved dev membership and the live claims-list response; no authority headers, request body, mutation, or fabricated records
- Authentication: real Entra bearer token held only in process memory and never written to the report
- Stop conditions: unexpected status, error rate above zero, or infrastructure instability

## Readiness-only results

| Run | Status | Error rate | Client p50 | Client p95 | Target p95 |
|---|---:|---:|---:|---:|---:|
| Initial | 50/50 `200` | 0% | 331 ms | 1,626 ms | 300 ms |
| Warm follow-up | 50/50 `200` | 0% | 319 ms | 1,781 ms | 300 ms |

Both runs failed the external p95 latency target. Application Insights reported 131 successful readiness requests in the diagnostic window with server-side p50 `0.2332 ms`, p95 `7.8275 ms`, p99 `30.822 ms`, and maximum `35.5932 ms`. This indicates the API endpoint itself was fast; the failed external measurement is dominated by client-to-Azure/front-end connection latency.

## Authenticated results

The authenticated live UAT prerequisite passed immediately before this run: readiness returned `200`, anonymous `/api/v1/me` returned `401`, the portal `/app` route returned `200`, and authenticated `/api/v1/me` returned `200` with server-resolved tenant, workspace, user, and role context.

| Scenario | Status | Error rate | Client p50 | Client p95 | Client p99 | Target p95 |
|---|---:|---:|---:|---:|---:|---:|
| `GET /health/ready` | 50/50 `200` | 0% | 292 ms | 1,183 ms | 1,201 ms | 300 ms |
| `GET /api/v1/claims` | 50/50 `200` | 0% | 526 ms | 1,277 ms | 1,463 ms | 800 ms |

Application Insights correlated 51 successful claims requests with server-side p50 `181.1053 ms`, p95 `961.8373 ms`, p99 `1,114.1079 ms`, and maximum `1,114.1079 ms`. It also correlated 70 successful readiness requests with server-side p95 `1.0569 ms`. The claims-list route therefore missed the provisional `800 ms` p95 target at the server as well as at the client; this is not solely a network or cold-connection effect.

## Optimization follow-up

Application Insights showed about seven SQL dependencies per authenticated claims request before optimization. The permission repository was changed to reuse the exact request-scoped effective-permission snapshot and avoid a redundant membership query. The full backend gate passed `376/376`, and Release deployment `b0b8d3d3-1bf4-4c22-976a-466d17516c41` completed `RuntimeSuccessful` with one successful instance and no failures.

Post-deployment telemetry showed exactly four SQL dependencies per observed claims request, with zero SQL failures. The first post-deployment run remained error-free but measured client claims p50 `426 ms`, p95 `2,849 ms`, and p99 `3,015 ms`. Its server p50 improved to `111.2216 ms`, while server p95 was `1,532.0427 ms`; the slowest `3,057.6774 ms` request spent only `63.2768 ms` in SQL, showing that most tail latency was outside SQL execution.

An immediate warm repeat also returned 50/50 `200` with client claims p50 `445 ms`, p95 `1,336 ms`, and p99 `5,944 ms`. Application Insights sampled 29 claims requests from that window at server p50 `111.1998 ms`, p95 `321.0767 ms`, and a `5,525.1093 ms` p99/max outlier. The optimization reduced database round trips and steady-state median/server p95, but did not make the external p95 gate pass.

The live dev host runs on one Linux `B1` App Service instance with `Always On=false`, HTTP/2 disabled, and no pre-warmed instances. The repository IaC now enables HTTP/2 and keeps non-dev protected runtimes warm, but those settings were not applied to the live dev host during this test.


## Decision

- Remote test approval blocker: resolved for the bounded dev readiness and authenticated claims-list scopes above.
- Authenticated execution blocker: resolved for `/api/v1/me` and claims-list reads.
- Dev external p95 gate: failed for readiness and claims list.
- API server-execution gate: passed for readiness.
- Claims-list server-execution gate: failed against the provisional `800 ms` p95 target.
- Staging/pilot performance sign-off: not granted.

Before staging sign-off, profile the claims query and database calls, confirm the target against pilot data volume, deploy the intended non-dev hosting configuration through change control, and rerun from an approved runner near the deployment region. Claim detail, document, provider, queue, mutation, resilience, and soak scenarios remain open.
