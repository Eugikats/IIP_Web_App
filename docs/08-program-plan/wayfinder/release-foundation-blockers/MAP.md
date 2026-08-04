# Release Foundation Blockers � Wayfinder Map

**Label:** `wayfinder:map`  
**Status:** Open  
**Tracker:** IIP-RF-001 � [GitHub #11](https://github.com/OticGroupAz/agenticIip-v2/issues/11) � [spec](../../../agents/specs/release-foundation-blocker-remediation.md)  
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

- Spec IIP-RF-001 published (`ready-for-agent`); tickets RF-01�RF-10 as GitHub #12�#21.
- [Enforce Server-Resolved Tenant Authority in Compliance](tickets/enforce-server-resolved-tenant-authority.md) � RF-01 � [#23](https://github.com/OticGroupAz/agenticIip-v2/pull/23) merged: `IComplianceQueryService` + membership `TenantContext` only.
- [Guarantee Human Review on Every Consequential Workflow Path](tickets/guarantee-human-review-on-every-path.md) � RF-02 � [#22](https://github.com/OticGroupAz/agenticIip-v2/pull/22) merged: all-paths HumanReview publish invariant + `ApprovalDecisionGuard`.
- [Implement Immutable Workflow Version Lineage](tickets/implement-immutable-workflow-version-lineage.md) � RF-03 � [#25](https://github.com/OticGroupAz/agenticIip-v2/pull/25) merged: key+version identity, audited draft/publish, runs pin DefinitionId.
- [Bound Orchestration to Release 1](tickets/bound-orchestration-to-release-one.md) � RF-04 � [#26](https://github.com/OticGroupAz/agenticIip-v2/pull/26) merged: R1 allow-list at publish/start/dispatch; R2�5 held.
- [Reinstate Merge Governance](tickets/reinstate-merge-governance.md) � RF-07 � [#24](https://github.com/OticGroupAz/agenticIip-v2/pull/24): private-repo plan blocks native rulesets (403) / classic required checks (404); compensating control in force � PR-only to `main`, green Backend/Portal/Infra from `.github/workflows/ci.yml`, ?1 maintainer approval; evidence in `docs/05-deployment-release-enablement/iip_merge_governance.md`.
- [Invalidate and Re-evaluate Downstream Workflow State](tickets/invalidate-downstream-workflow-state.md) � RF-05 � branch `harry/rf-invalidate-downstream-after-corrections`: BFS descendant invalidation + re-queue after field correction / return-for-correction; audits `nodes.invalidated` / `nodes.reevaluated`.
- [Deliver the Release 1 Workflow Execution Worker](tickets/deliver-release-one-workflow-worker.md) � RF-06 � branch `harry/rf-invalidate-downstream-after-corrections`: Service Bus worker contract, server-resolved membership per message, idempotent duplicate/stale guards, and bounded R1 node execution tests; pending deployed telemetry proof.
- [Close Azure SQL and Document Provider Gates](tickets/close-azure-sql-and-document-provider-gates.md) � RF-08 � branch `harry/rf-invalidate-downstream-after-corrections`: provider health now surfaces explicit `not_configured` for intentionally disabled providers; live migration/capacity evidence remains human-blocked.
- [Pass Authenticated UAT and Tail-Latency Gates](tickets/pass-uat-and-tail-latency-gates.md) � RF-09 � branch `harry/rf-invalidate-downstream-after-corrections`: UAT/perf smoke runners emit richer RF-09 evidence and fail fast with explicit blockers; authenticated staging reruns and tail-latency remediation remain open.

## Frontier

Open / unblocked next work:

- [Deliver the Release 1 Workflow Execution Worker](tickets/deliver-release-one-workflow-worker.md) � RF-06 � [#18](https://github.com/OticGroupAz/agenticIip-v2/issues/18) (implementation in progress; deployment evidence pending)
- [Close Azure SQL and Document Provider Gates](tickets/close-azure-sql-and-document-provider-gates.md) � RF-08 � [#19](https://github.com/OticGroupAz/agenticIip-v2/issues/19) (app-side safety done; operator migration/capacity evidence pending)
- [Pass Authenticated UAT and Tail-Latency Gates](tickets/pass-uat-and-tail-latency-gates.md) � RF-09 � [#20](https://github.com/OticGroupAz/agenticIip-v2/issues/20) (tooling/evidence progress in place; authenticated staging evidence and latency remediation pending)

## Blocked tickets

- [Publish Final Go-No-Go Evidence](tickets/publish-final-go-no-go-evidence.md) � RF-10 � blocked by every remediation and verification ticket

## Not yet specified

- Whether the current sponsored subscription can meet representative non-dev latency and provider-capability gates without PAYG approval.
- When the org will upgrade GitHub plan (or otherwise) so native `main` required checks/rulesets can replace the RF-07 compensating control.
- Which authorized operator and maintenance window will apply the consolidated Azure SQL migration.
- Which real Azure AI Document Intelligence deployment and region will support Release 1 after capacity/residency verification.

## Out of scope

- Partner Center or Managed Application republication.
- Production launch or insurer onboarding before staging and pilot gates pass.
- Extending Releases 2�5, Fraud, Underwriting, Billing, Marketplace, or other post-MVP surfaces.
- Visual redesign unrelated to blocker remediation.
