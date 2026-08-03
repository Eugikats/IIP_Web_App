# Release Foundation Blockers — Wayfinder Map

**Tracker:** IIP-RF-001 · [GitHub #11](https://github.com/OticGroupAz/agenticIip-v2/issues/11)  
**Local tickets:** `docs/08-program-plan/wayfinder/release-foundation-blockers/tickets/`

## Destination

Release foundation blockers remediated so CI green means merge-safe Release 1 readiness: server-resolved tenant authority, unavoidable human review, immutable workflow lineage, real worker, merge governance, and honest Azure/UAT evidence.

## Notes

Authority: `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CLAUDE.md`. One non-research ticket per session. Primary test seam: HTTP API tenant-isolation in `InsuranceIntelligence.Api.Tests`.

## Decisions so far

- [Enforce Server-Resolved Tenant Authority in Compliance](tickets/enforce-server-resolved-tenant-authority.md) — Compliance uses `IComplianceQueryService` with membership `TenantContext` only; header/query fallback removed; Aggregation in Application; API thin; `ComplianceTenantAuthorityTests` green.

## Frontier

Open / unblocked next work (not yet in Decisions so far):

- [Human review on every consequential workflow path](../../../../.scratch/release-foundation-blocker-remediation/issues/02-human-review-every-consequential-path.md) — RF-02 · [#13](https://github.com/OticGroupAz/agenticIip-v2/issues/13)
- [Immutable workflow version lineage](tickets/implement-immutable-workflow-version-lineage.md) — RF-03 · [#14](https://github.com/OticGroupAz/agenticIip-v2/issues/14)
- [Bound orchestration to Release 1](tickets/bound-orchestration-to-release-one.md) — RF-04 · [#15](https://github.com/OticGroupAz/agenticIip-v2/issues/15)
- [Reinstate merge governance](tickets/reinstate-merge-governance.md) — RF-07 · [#16](https://github.com/OticGroupAz/agenticIip-v2/issues/16)

Blocked (listed for orientation; not frontier-takeable):

- RF-05 invalidate downstream — blocked by RF-02
- RF-06 Release 1 worker — blocked by RF-02–05
- RF-08 Azure SQL / Document Intelligence gates — blocked by RF-06 + ops
- RF-09 Authenticated UAT / tail latency — blocked by RF-01, 06, 08
- RF-10 Final go/no-go evidence — blocked by RF-01–09

## Not yet specified

Human-owned unknowns (sponsored subscription latency, GitHub branch-protection plan limits, Azure SQL migration window, Document Intelligence region/capacity) remain operator-gated.

## Out of scope

Partner Center / Managed Application republication; Releases 2–5; visual redesign unrelated to blockers.
