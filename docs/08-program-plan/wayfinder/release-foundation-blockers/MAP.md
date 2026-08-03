# Wayfinder Map: Release Foundation Blocker Remediation

**Label:** `wayfinder:map`  
**Status:** Open  
**Tracker:** Local Markdown at `docs/agents/issue-tracker.md` (IIP-RF-001). Spec: `docs/agents/specs/release-foundation-blocker-remediation.md`. Branch: `harry/rf-enforce-server-tenant-authority`.

## Destination

Bring the release-foundation work now reachable from `main` to a demonstrably merge-safe and Release 1 dev-gate-ready state: server-resolved tenant authority, unavoidable human review, correct correction replay, immutable workflow version lineage, a real tenant-scoped execution worker, enforced release sequencing, passing CI, and truthful Azure/UAT/performance evidence. External dependencies must be explicitly closed or recorded as human-owned blockers; none may be disguised as completed.

## Notes

- The user explicitly requested resolution of all blockers, so this effort carries implementation and verification through the map rather than ending at a planning handoff.
- `AGENTS.md` is the highest authority. Also consult `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CLAUDE.md`, `README.md`, the consolidated roadmap, and the Bevaya-parity release blueprint before each implementation ticket.
- Work one non-research ticket per Wayfinder session. Claim it before changing code and record its resolution in the ticket before closing it.
- New implementation branches use the repository-required `harry/` prefix and start from current `origin/main`.
- Preserve unrelated untracked user files. Do not republish the Managed Application offer.
- Protected runtimes remain zero-simulation and fail closed. Consequential decisions and external actions always require accountable human review.
- Azure changes use least privilege, approved resources, live verification, and rollback or honest `Not configured` behavior.

## Decisions so far

- Spec IIP-RF-001 published (`ready-for-agent`); primary test seam = HTTP API tenant-isolation contract tests; first ticket branch `harry/rf-enforce-server-tenant-authority`.
- Tickets RF-01…RF-10 published (`ready-for-agent`): GitHub #12–#21; local `.scratch/release-foundation-blocker-remediation/issues/`. Frontier: RF-01, 02, 03, 04, 07.
- RF-02: all-paths HumanReview publish invariant + `ApprovalDecisionGuard` Decide/Send/Execute ceiling; branch `harry/rf-human-review-every-path`.

## Not yet specified

- Whether the current sponsored subscription can meet representative non-dev latency and provider-capability gates without PAYG approval.
- Whether GitHub plan limitations require an alternative to native protected-branch required checks.
- Which authorized operator and maintenance window will apply the consolidated Azure SQL migration.
- Which real Azure AI Document Intelligence deployment and region will support Release 1 after capacity/residency verification.

## Out of scope

- Partner Center or Managed Application republication.
- Production launch or insurer onboarding before staging and pilot gates pass.
- Extending Releases 2-5, Fraud, Underwriting, Billing, Marketplace, or other post-MVP surfaces.
- Visual redesign unrelated to blocker remediation.

## Frontier

- [Enforce Server-Resolved Tenant Authority in Compliance](tickets/enforce-server-resolved-tenant-authority.md)
- [Guarantee Human Review on Every Consequential Workflow Path](tickets/guarantee-human-review-on-every-path.md)
- [Implement Immutable Workflow Version Lineage](tickets/implement-immutable-workflow-version-lineage.md)
- [Bound Orchestration to Release 1](tickets/bound-orchestration-to-release-one.md)
- [Reinstate Merge Governance](tickets/reinstate-merge-governance.md)

## Blocked tickets

- [Invalidate and Re-evaluate Downstream Workflow State](tickets/invalidate-downstream-workflow-state.md) — blocked by human-review invariant.
- [Deliver the Release 1 Workflow Execution Worker](tickets/deliver-release-one-workflow-worker.md) — blocked by review, replay, versioning, and Release 1 scope decisions.
- [Close Azure SQL and Document Provider Gates](tickets/close-azure-sql-and-document-provider-gates.md) — blocked by the worker contract and external operator/capacity facts.
- [Pass Authenticated UAT and Tail-Latency Gates](tickets/pass-uat-and-tail-latency-gates.md) — blocked by tenant safety, worker, and real-provider path.
- [Publish Final Go-No-Go Evidence](tickets/publish-final-go-no-go-evidence.md) — blocked by every remediation and verification ticket.
