# Release Foundation Blockers — Wayfinder Map

**Label:** `wayfinder:map`  
**Status:** Open  
**Tracker:** IIP-RF-001 · [GitHub #11](https://github.com/OticGroupAz/agenticIip-v2/issues/11) · [spec](../../../agents/specs/release-foundation-blocker-remediation.md)  
**Local tickets:** `docs/08-program-plan/wayfinder/release-foundation-blockers/tickets/`

## Destination

Bring the release-foundation work now reachable from `main` to a demonstrably merge-safe and Release 1 dev-gate-ready state: server-resolved tenant authority, unavoidable human review, correct correction replay, immutable workflow version lineage, a real tenant-scoped execution worker, enforced release sequencing, passing CI, and truthful Azure/UAT/performance evidence. External dependencies must be explicitly closed or recorded as human-owned blockers; none may be disguised as completed.

## Notes

- `AGENTS.md` is the highest authority. Also consult `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CLAUDE.md`, `README.md`, the consolidated roadmap, and the Bevaya-parity release blueprint before each implementation ticket.
- Work one non-research ticket per Wayfinder session. Claim it before changing code and record its resolution in the ticket before closing it.
- New implementation branches use the repository-required `harry/` prefix and start from current `origin/main`.
- Preserve unrelated untracked user files. Do not republish the Managed Application offer.
- Protected runtimes remain zero-simulation and fail closed. Consequential decisions and external actions always require accountable human review.
- Primary test seam: HTTP API tenant-isolation / contract tests in `InsuranceIntelligence.Api.Tests`.

## Decisions so far

- Spec IIP-RF-001 published (`ready-for-agent`); tickets RF-01…RF-10 as GitHub #12–#21.
- [Enforce Server-Resolved Tenant Authority in Compliance](tickets/enforce-server-resolved-tenant-authority.md) — RF-01 · [#23](https://github.com/OticGroupAz/agenticIip-v2/pull/23): `IComplianceQueryService` + membership `TenantContext` only; header/query fallback removed; `ComplianceTenantAuthorityTests` green.
- [Guarantee Human Review on Every Consequential Workflow Path](tickets/guarantee-human-review-on-every-path.md) — RF-02 · [#22](https://github.com/OticGroupAz/agenticIip-v2/pull/22) merged: all-paths HumanReview publish invariant + `ApprovalDecisionGuard` Decide/Send/Execute ceiling.

## Frontier

Open / unblocked next work:

- [Implement Immutable Workflow Version Lineage](tickets/implement-immutable-workflow-version-lineage.md) — RF-03 · [#14](https://github.com/OticGroupAz/agenticIip-v2/issues/14)
- [Bound Orchestration to Release 1](tickets/bound-orchestration-to-release-one.md) — RF-04 · [#15](https://github.com/OticGroupAz/agenticIip-v2/issues/15)
- [Reinstate Merge Governance](tickets/reinstate-merge-governance.md) — RF-07 · [#16](https://github.com/OticGroupAz/agenticIip-v2/issues/16)

## Blocked tickets

- [Invalidate and Re-evaluate Downstream Workflow State](tickets/invalidate-downstream-workflow-state.md) — RF-05 · blocked by RF-02 (code landed; unblocks after RF-01 merge hygiene)
- [Deliver the Release 1 Workflow Execution Worker](tickets/deliver-release-one-workflow-worker.md) — RF-06 · blocked by RF-02–05 + Release 1 scope
- [Close Azure SQL and Document Provider Gates](tickets/close-azure-sql-and-document-provider-gates.md) — RF-08 · blocked by RF-06 + ops facts
- [Pass Authenticated UAT and Tail-Latency Gates](tickets/pass-uat-and-tail-latency-gates.md) — RF-09 · blocked by tenant safety, worker, real-provider path
- [Publish Final Go-No-Go Evidence](tickets/publish-final-go-no-go-evidence.md) — RF-10 · blocked by every remediation and verification ticket

## Not yet specified

- Whether the current sponsored subscription can meet representative non-dev latency and provider-capability gates without PAYG approval.
- Whether GitHub plan limitations require an alternative to native protected-branch required checks.
- Which authorized operator and maintenance window will apply the consolidated Azure SQL migration.
- Which real Azure AI Document Intelligence deployment and region will support Release 1 after capacity/residency verification.

## Out of scope

- Partner Center or Managed Application republication.
- Production launch or insurer onboarding before staging and pilot gates pass.
- Extending Releases 2–5, Fraud, Underwriting, Billing, Marketplace, or other post-MVP surfaces.
- Visual redesign unrelated to blocker remediation.
