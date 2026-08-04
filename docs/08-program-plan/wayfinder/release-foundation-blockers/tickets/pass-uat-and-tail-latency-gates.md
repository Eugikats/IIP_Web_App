# Pass Authenticated UAT and Tail-Latency Gates

**Label:** `wayfinder:task`  
**Status:** In progress  
**Claim:** harry/rf-invalidate-downstream-after-corrections  
**Blocked by:** Enforce Server-Resolved Tenant Authority in Compliance; Deliver the Release 1 Workflow Execution Worker; Close Azure SQL and Document Provider Gates

## Question

What measured changes and representative environment are required to pass the authenticated role/provider UAT matrix and eliminate the claims-list tail-latency failure without relaxing thresholds or hiding outliers?

## Resolution criteria

- Root cause of SQL/dependency and host tail latency identified with telemetry.
- Any approved latency remediation preserves tenant isolation and authorization.
- Representative rerun passes documented p95/p99 and error-rate thresholds.
- Full role matrix, provider health, recovery, and low-bandwidth UAT pass with retained evidence.

## Progress notes

- The repo-owned UAT smoke runner now supports machine-readable JSON output plus authenticated route assertions for claims, provider status, compliance posture, and denied-route checks.
- The repo-owned performance smoke runner now supports representative authenticated scenarios beyond claims list: provider status and compliance posture.
- Local validation/evidence completed:
  - focused claims query tests passed;
  - performance runner self-test passed and wrote `artifacts/rf09/perf-self-test.json`;
  - blocked live smoke wrote `artifacts/rf09/uat-smoke-blocked.json`, confirming readiness/anonymous-denial/portal reachability while authenticated checks remain token-gated.
- No safe code-side claims-list latency remediation was committed in this slice; the representative p95/p99 gate remains open pending further telemetry-guided profiling in the approved environment.
- Remaining human/environment blockers:
  - authenticated multi-role token runs in approved dev/staging;
  - representative staging/provider rerun with real providers;
  - p95/p99 acceptance evidence after rerun on approved environment near the deployment region.
