# IIP Local Issue Tracker

**Fallback:** Used while a dedicated tracker index is not otherwise provisioned.  
**Triage labels:** `ready-for-agent` | `in-progress` | `blocked` | `done`

| ID | Title | Triage | Spec / ticket | Branch | Notes |
|---|---|---|---|---|---|
| IIP-RF-001 | Release Foundation Blocker Remediation | `ready-for-agent` | [spec](./specs/release-foundation-blocker-remediation.md) Â· [GitHub #11](https://github.com/OticGroupAz/agenticIip-v2/issues/11) | `harry/rf-enforce-server-tenant-authority` | Parent epic. Child tickets RF-01â€¦RF-10 below. |
| RF-01 | Server-resolved tenant authority for Compliance | `done` | [local](../../.scratch/release-foundation-blocker-remediation/issues/01-server-resolved-tenant-authority-compliance.md) Â· [#12](https://github.com/OticGroupAz/agenticIip-v2/issues/12) | `harry/rf-enforce-server-tenant-authority` | Done — Compliance membership TenantContext only |
| RF-02 | Human review on every consequential workflow path | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/02-human-review-every-consequential-path.md) Â· [#13](https://github.com/OticGroupAz/agenticIip-v2/issues/13) | â€” | Frontier |
| RF-03 | Immutable workflow version lineage and run pinning | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/03-immutable-workflow-version-lineage.md) Â· [#14](https://github.com/OticGroupAz/agenticIip-v2/issues/14) | â€” | Frontier |
| RF-04 | Bound orchestration to Release 1 | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/04-bound-orchestration-to-release-1.md) Â· [#15](https://github.com/OticGroupAz/agenticIip-v2/issues/15) | â€” | Frontier (before worker) |
| RF-05 | Invalidate/re-evaluate downstream after corrections | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/05-invalidate-downstream-after-corrections.md) Â· [#17](https://github.com/OticGroupAz/agenticIip-v2/issues/17) | â€” | Blocked by RF-02 |
| RF-06 | Release 1 workflow execution worker | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/06-release-1-workflow-execution-worker.md) Â· [#18](https://github.com/OticGroupAz/agenticIip-v2/issues/18) | â€” | Blocked by RF-02â€¦05 |
| RF-07 | Reinstate merge governance | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/07-reinstate-merge-governance.md) Â· [#16](https://github.com/OticGroupAz/agenticIip-v2/issues/16) | â€” | Frontier |
| RF-08 | Close Azure SQL and Document Intelligence gates | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/08-close-azure-sql-and-document-provider-gates.md) Â· [#19](https://github.com/OticGroupAz/agenticIip-v2/issues/19) | â€” | Blocked by RF-06 + ops facts |
| RF-09 | Authenticated UAT and tail-latency gates | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/09-authenticated-uat-and-tail-latency.md) Â· [#20](https://github.com/OticGroupAz/agenticIip-v2/issues/20) | â€” | Blocked by RF-01, 06, 08 |
| RF-10 | Publish final go/no-go evidence | `ready-for-agent` | [local](../../.scratch/release-foundation-blocker-remediation/issues/10-publish-final-go-no-go-evidence.md) Â· [#21](https://github.com/OticGroupAz/agenticIip-v2/issues/21) | â€” | Blocked by RF-01â€¦09 |

## Label vocabulary

| Label | Meaning |
|---|---|
| `ready-for-agent` | Spec/ticket accepted; agent may claim and implement |
| `in-progress` | Claimed; implementation underway |
| `blocked` | Waiting on human-owned Azure/legal/plan facts |
| `done` | Resolution criteria met with evidence |

Wayfinder ticket labels (`wayfinder:map`, `wayfinder:prototype`, `wayfinder:grilling`, `wayfinder:task`) remain on individual tickets under `docs/08-program-plan/wayfinder/release-foundation-blockers/`.
