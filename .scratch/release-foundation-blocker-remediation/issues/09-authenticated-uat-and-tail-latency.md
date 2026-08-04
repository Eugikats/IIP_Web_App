# 09 — Authenticated UAT and tail-latency gates

**What to build:** Authenticated UAT exercises Release 1 paths against real staging providers. Tail latency on representative paths is measured; sponsored-subscription limits are stated honestly if they prevent meeting non-dev gates.

**Blocked by:** 01, 06, 08.

**Status:** in-progress

- [ ] Authenticated UAT scripts/results against staging with real providers
- [ ] Tail-latency measurements recorded for representative paths
- [ ] Capability gaps listed without fake green

## Progress

- `tools/qa/Invoke-IipLiveUatSmoke.ps1` now supports JSON evidence output, explicit timeout, authenticated route checks, and denied-route assertions.
- `tools/qa/Invoke-IipPerformanceSmoke.mjs` now supports representative authenticated provider/compliance scenarios plus JSON threshold-failure summaries.
- Local evidence generated:
  - `artifacts/rf09/perf-self-test.json`
  - `artifacts/rf09/uat-smoke-blocked.json`
- No safe claims-list latency fix is claimed in this slice; the tail-latency gate remains open for further telemetry-guided work in the approved environment.

## Remaining blockers

- Approved authenticated tokens for the full role matrix in the representative environment.
- Staging or approved non-dev rerun with real providers and synthetic/masked records.
- Final p95/p99 pass evidence after rerun near the deployment region.
