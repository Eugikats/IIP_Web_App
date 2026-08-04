# 08 — Close Azure SQL and Document Intelligence gates

**What to build:** Consolidated Azure SQL schema is applied in a recorded operator window, and Document Intelligence is live-proven for Release 1—or each gap is recorded as a human-owned blocker / Not configured. No fake provider success.

**Blocked by:** 06 — Release 1 workflow execution worker; plus human-owned operator/capacity facts.

**Status:** in-progress

- [ ] Migration applied with evidence, or blocker named (operator/window)
- [ ] Document Intelligence deployment/region/capacity verified, or blocker named
- [x] Failures surface as Not configured / safe errors — never simulated success

## Progress

- Provider health now reports explicit `not_configured` status when a provider is intentionally disabled/not configured in governed environments.
- Added contract coverage proving `documentIntelligence`, `aiSearch`, and `azureOpenAI` expose `not_configured` instead of ambiguous missing states.

## Remaining blocker evidence (human-owned)

- Azure SQL migration operator window and applied-history capture.
- Azure AI Document Intelligence deployment, region, and capacity verification for Release 1.
- Live staged proof for upload -> extraction -> correction/re-evaluation.
