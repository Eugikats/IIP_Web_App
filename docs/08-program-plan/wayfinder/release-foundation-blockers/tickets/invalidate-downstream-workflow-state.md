# Invalidate and Re-evaluate Downstream Workflow State

**Label:** `wayfinder:prototype`  
**Status:** Done  
**Claim:** harry/rf-invalidate-downstream-after-corrections  
**Blocked by:** Guarantee Human Review on Every Consequential Workflow Path (satisfied — RF-02 merged)

## Question

When a reviewer returns work for correction, which completed descendants must be invalidated and replayed so no quality, readiness, triage, draft, or approval result can survive changed upstream evidence?

## Resolution criteria

- General descendant-invalidation rule defined for the workflow graph.
- Release 1 correction resets document check, quality gate, review, and every affected downstream result.
- Prior outputs remain auditable but cannot be treated as current.
- Idempotency and concurrent-review tests pass.

## Resolution

- Added `AgentWorkflowDescendantInvalidation` (BFS root + descendants; `ResolveEvidenceCorrectionRoot` from HumanReview `correctionTarget`).
- `AgentWorkflowService.InvalidateDownstreamAfterEvidenceCorrectionAsync` resets active/completed runs for a business reference, cancels open work items, audits prior node snapshots (`agent_workflow.nodes.invalidated` / `reevaluated`), then `ActivateReadyNodesAsync` re-queues the correction root.
- `ReturnForCorrection` uses the same descendant invalidation (no longer resets only review + target).
- `POST .../correct-field` calls invalidation for `case` / caseId after a successful field correction.
- Contract tests: `InvalidateDownstreamAfterCorrectionContractTests`; unit tests for the BFS helper.
