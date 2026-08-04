# IIP Local Issue Tracker

**Fallback:** Used while a dedicated tracker index is not otherwise provisioned.  
**Triage labels:** `ready-for-agent` | `in-progress` | `blocked` | `done`

| ID | Title | Triage | Spec / ticket | Branch | Notes |
|---|---|---|---|---|---|
| IIP-RF-001 | Release Foundation Blocker Remediation | `in-progress` | [spec](./specs/release-foundation-blocker-remediation.md) · [GitHub #11](https://github.com/OticGroupAz/agenticIip-v2/issues/11) | `harry/rf-invalidate-downstream-after-corrections` | Parent epic. Agent-side RF-10 pack published; RF-06/08/09 human-blocked. |
| RF-01 | Server-resolved tenant authority for Compliance | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/01-server-resolved-tenant-authority-compliance.md) · [#12](https://github.com/OticGroupAz/agenticIip-v2/issues/12) | `harry/rf-enforce-server-tenant-authority` | Done — Compliance membership TenantContext only |
| RF-02 | Human review on every consequential workflow path | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/02-human-review-every-consequential-path.md) · [#13](https://github.com/OticGroupAz/agenticIip-v2/issues/13) | `harry/rf-human-review-every-path` | Merged [#22](https://github.com/OticGroupAz/agenticIip-v2/pull/22) |
| RF-03 | Immutable workflow version lineage and run pinning | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/03-immutable-workflow-version-lineage.md) · [#14](https://github.com/OticGroupAz/agenticIip-v2/issues/14) | `harry/rf-immutable-workflow-version-lineage` | Merged [#25](https://github.com/OticGroupAz/agenticIip-v2/pull/25) |
| RF-04 | Bound orchestration to Release 1 | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/04-bound-orchestration-to-release-1.md) · [#15](https://github.com/OticGroupAz/agenticIip-v2/issues/15) | `harry/rf-bound-orchestration-release-1` | Merged [#26](https://github.com/OticGroupAz/agenticIip-v2/pull/26) |
| RF-05 | Invalidate/re-evaluate downstream after corrections | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/05-invalidate-downstream-after-corrections.md) · [#17](https://github.com/OticGroupAz/agenticIip-v2/issues/17) | `harry/rf-invalidate-downstream-after-corrections` | Code-complete on branch; prior [#27](https://github.com/OticGroupAz/agenticIip-v2/pull/27) |
| RF-06 | Release 1 workflow execution worker | `blocked` | [local](../../.scratch/release-foundation-blocker-remediation/issues/06-release-1-workflow-execution-worker.md) · [#18](https://github.com/OticGroupAz/agenticIip-v2/issues/18) | `harry/rf-invalidate-downstream-after-corrections` | Code + tests done; **human:** deploy telemetry |
| RF-07 | Reinstate merge governance | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/07-reinstate-merge-governance.md) · [#16](https://github.com/OticGroupAz/agenticIip-v2/issues/16) | `harry/rf-reinstate-merge-governance` | Merged [#24](https://github.com/OticGroupAz/agenticIip-v2/pull/24); compensating control |
| RF-08 | Close Azure SQL and Document Intelligence gates | `blocked` | [local](../../.scratch/release-foundation-blocker-remediation/issues/08-close-azure-sql-and-document-provider-gates.md) · [#19](https://github.com/OticGroupAz/agenticIip-v2/issues/19) | `harry/rf-invalidate-downstream-after-corrections` | App-side NotConfigured done; **human:** migration/capacity |
| RF-09 | Authenticated UAT and tail-latency gates | `blocked` | [local](../../.scratch/release-foundation-blocker-remediation/issues/09-authenticated-uat-and-tail-latency.md) · [#20](https://github.com/OticGroupAz/agenticIip-v2/issues/20) | `harry/rf-invalidate-downstream-after-corrections` | Harness done; **human:** Entra UAT + p95/p99 |
| RF-10 | Publish final go/no-go evidence | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/10-publish-final-go-no-go-evidence.md) · [#21](https://github.com/OticGroupAz/agenticIip-v2/issues/21) · [evidence](../07-qa/rf10-release-foundation-go-no-go-evidence.md) | `harry/rf-invalidate-downstream-after-corrections` | Honest NO-GO pack; PRODUCTION-CANDIDATE **NO** |

## Label vocabulary

| Label | Meaning |
|---|---|
| `ready-for-agent` | Spec/ticket accepted; agent may claim and implement |
| `in-progress` | Claimed; implementation underway |
| `blocked` | Waiting on human-owned Azure/legal/plan facts |
| `done` | Resolution criteria met with evidence |

Wayfinder ticket labels (`wayfinder:map`, `wayfinder:prototype`, `wayfinder:grilling`, `wayfinder:task`) remain on individual tickets under `docs/08-program-plan/wayfinder/release-foundation-blockers/`.
