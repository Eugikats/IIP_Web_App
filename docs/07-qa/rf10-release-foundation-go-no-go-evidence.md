# RF-10 — Release Foundation Final Go / No-Go Evidence

| Field | Value |
|---|---|
| Ticket | RF-10 · [publish-final-go-no-go-evidence](../08-program-plan/wayfinder/release-foundation-blockers/tickets/publish-final-go-no-go-evidence.md) · [GitHub #21](https://github.com/OticGroupAz/agenticIip-v2/issues/21) |
| Branch | `harry/rf-invalidate-downstream-after-corrections` |
| Tip (at publish) | `7afa7c8` (+ RF-10 docs commit on same branch) |
| Authority | `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, zero-simulation rule |
| Decision date | 2026-08-04 |
| Consolidated readiness record | [`release-1-candidate-readiness-record.md`](./release-1-candidate-readiness-record.md) — branch tip, `/api/infra-contracts/known-gaps` status, known-gaps registry, motor scope |

## Verdict (evidence-backed only)

```text
RELEASE 1 DEV-GATE: NO — code remediation progressed; human-owned Azure/UAT/latency gates remain open
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

No live Azure deployment, Partner Center, or Managed Application evidence was produced in this agent session. Thresholds were not relaxed. Simulated or unverified results are not treated as passing.

## Decision matrix

| Readiness class | Status | Basis |
|---|---|---|
| Merge-safe (RF code on this branch) | **Conditional YES** | RF-01…05 and RF-07 merged or coded with tests; RF-06/08/09 app-side changes have contract/unit/tooling evidence. Still requires PR CI green + RF-07 compensating merge governance. |
| Dev-ready (local / LocalTest) | **Partial** | Worker, invalidation, NotConfigured surfacing, and smoke harnesses exist. Not a substitute for deployed provider proof. |
| Staging-ready | **NO** | Missing deployed worker telemetry, SQL migration window evidence, Doc Intelligence capacity proof, authenticated multi-role UAT, representative p95/p99. |
| Pilot-ready | **NO** | Blocked by staging-ready gaps and human operational gates. |
| Production / Marketplace-ready | **NO** | Explicit; no Gate 5/6 evidence pack exists for this branch tip. |

## Code-complete on this branch (agent-deliverable)

Evidence is repository code + automated tests / harnesses only—not live Azure run results.

| ID | Item | Evidence (repo) | Status |
|---|---|---|---|
| RF-01 | Server-resolved tenant authority (Compliance) | Merged [#23](https://github.com/OticGroupAz/agenticIip-v2/pull/23) | Done |
| RF-02 | Human review on every consequential path | Merged [#22](https://github.com/OticGroupAz/agenticIip-v2/pull/22) | Done |
| RF-03 | Immutable workflow version lineage | Merged [#25](https://github.com/OticGroupAz/agenticIip-v2/pull/25) | Done |
| RF-04 | Bound orchestration to Release 1 | Merged [#26](https://github.com/OticGroupAz/agenticIip-v2/pull/26) | Done |
| RF-05 | Invalidate/re-evaluate downstream after corrections | `AgentWorkflowDescendantInvalidation`, `InvalidateDownstreamAfterCorrectionContractTests`; prior [#27](https://github.com/OticGroupAz/agenticIip-v2/pull/27) | Code-complete on branch |
| RF-06 | Release 1 workflow execution worker (app-side) | `AgentWorkflowExecutionWorker`, Function host wiring, `AgentWorkflowExecutionWorkerContractTests` | **Code-complete; deploy telemetry human-blocked** |
| RF-07 | Merge governance compensating control | `docs/05-deployment-release-enablement/iip_merge_governance.md`; merged [#24](https://github.com/OticGroupAz/agenticIip-v2/pull/24) | Done (plan-limited compensating control) |
| RF-08 | Provider NotConfigured surfacing (app-side) | Provider health / Phase7 tests for explicit `not_configured` | **App-side complete; SQL/Doc Intelligence live gates human-blocked** |
| RF-09 | UAT / latency smoke harnesses | `tools/qa/Invoke-IipLiveUatSmoke.ps1`, `Invoke-IipPerformanceSmoke.mjs`; blocked smoke artifact path documented on ticket | **Harness progress; authenticated UAT + p95/p99 human-blocked** |
| RF-10 | This evidence pack | This document + Wayfinder / tracker updates | Done as honest NO-GO pack |

## Human-owned blockers (not disguised as done)

| ID | Blocker | Owner | Why it remains open |
|---|---|---|---|
| RF-06 | Deployed worker + Application Insights / Log Analytics proof of one real bounded R1 execution | Azure operators | No deployed telemetry capture in coding sessions |
| RF-08 | Authorized Azure SQL migration window + applied-history / rollback record | DB / platform owners | Requires maintenance window and credentials |
| RF-08 | Azure AI Document Intelligence region/capacity/residency proof | Azure / capacity owners | Capacity and residency cannot be invented |
| RF-08 | Live upload → extraction → correction → re-evaluation on real providers | Staging operators | Zero-simulation; needs configured staging |
| RF-09 | Authenticated multi-role Entra UAT tokens and retained matrix evidence | Identity / UAT leads | Tokens and role matrix are human-provisioned |
| RF-09 | Representative staging p95/p99 vs documented thresholds (no relaxation) | Perf / platform | Prior local/blocked smokes ≠ representative gate pass |
| RF-07 residual | Native GitHub rulesets / required checks | Org admins | Private-repo plan still blocks native enforcement; compensating control remains |
| Cross-cutting | Sponsored subscription / PAYG for non-dev latency & provider capability | Commercial / Azure billing | Recorded on Wayfinder map as unspecified |

## What this pack does *not* claim

- Branch tip equals a deployed staging or production commit (parity not proven here).
- CI on the open PR is green until checks actually run.
- Any RF-06/08/09 resolution criterion that requires live Azure, Entra tokens, or Partner Center action.
- Production Release Program Gates 2–6 complete.

## Required human next actions (ordered)

1. Open/land PR for this branch under RF-07 compensating merge rules (green Backend/Portal/Infra + ≥1 maintainer approval).
2. Deploy Functions worker to approved env; capture bounded R1 execution telemetry (RF-06).
3. Schedule SQL migration; record applied history and rollback (RF-08).
4. Confirm Document Intelligence deployment/region/capacity; run live document path (RF-08).
5. Provision multi-role Entra tokens; run authenticated UAT matrix; retain evidence (RF-09).
6. Run representative authenticated perf near deployment region; meet documented p95/p99 without threshold changes (RF-09).
7. Re-issue RF-10 only after items 2–6 have linked evidence—then reconsider staging / pilot readiness.

## Release readiness stamp

```text
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

Until human-owned blockers above are closed with real evidence, the accountable decision for Release 1 staging/pilot/production is **NO-GO**.
